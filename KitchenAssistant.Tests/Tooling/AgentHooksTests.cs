using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace KitchenAssistant.Tests.Tooling;

// Proves the Claude Code hooks in .claude/hooks/ (configured in .claude/settings.json): each case pipes a hook payload
// into the real script and asserts the exit code and the channel. Exit 2 + stderr is the only signal Claude sees.
// Runs in a throwaway git repo; only `dotnet` is replaced (HOOK_DOTNET), by a stub that flags marker strings:
// BROKEN in a .cs file is a compile error, FAILING_ASSERT in a *Tests.cs file a failing test, PENDING_MODEL in Data/
// a model change without a migration. The end-of-turn hook runs this class only when .claude/hooks/ changed.
[Trait("Category", "AgentHooks")]
public sealed class AgentHooksTests : IDisposable
{
    private readonly HookSandbox _box = new();

    public void Dispose() => _box.Dispose();

    [Fact]
    public void Per_edit_reports_a_compile_error_in_the_edited_file()
    {
        _box.Write("Recipes/Thing.cs", "public class Thing { int x = BROKEN; }");

        var r = _box.PerEdit(_box.Path("Recipes/Thing.cs"));

        Assert.Equal(2, r.Exit);
        Assert.Contains("Build errors in Recipes/Thing.cs", r.Stderr);
        Assert.Contains("CS0103", r.Stderr);
    }

    [Fact]
    public void Per_edit_reports_a_failing_related_test()
    {
        _box.Write("KitchenAssistant.Tests/Recipes/ThingTests.cs", "public class ThingTests { Thing t; /* FAILING_ASSERT */ }");

        var r = _box.PerEdit(_box.Path("Recipes/Thing.cs"));

        Assert.Equal(2, r.Exit);
        Assert.Contains("Tests related to Recipes/Thing.cs fail (ThingTests)", r.Stderr);
        Assert.Contains("Failed KitchenAssistant.Tests.ThingTests", r.Stderr);
    }

    [Fact]
    public void Per_edit_on_a_clean_file_passes_and_runs_only_its_related_tests()
    {
        var r = _box.PerEdit(_box.Path("Recipes/Thing.cs"));

        Assert.Equal(0, r.Exit);
        Assert.Equal("", r.Stderr);
        var test = Assert.Single(_box.DotnetCalls(), c => c.Contains("|test "));
        Assert.Contains("--filter FullyQualifiedName~ThingTests", test);
        Assert.DoesNotContain("OtherTests", test);
    }

    [Fact]
    public void Per_edit_with_an_error_only_in_another_file_adds_non_blocking_context()
    {
        _box.Write("Recipes/Other.cs", "public class Other { int y = BROKEN; }");

        var r = _box.PerEdit(_box.Path("Recipes/Thing.cs"));

        Assert.Equal(0, r.Exit);
        using var json = JsonDocument.Parse(r.Stdout);
        var context = json.RootElement.GetProperty("hookSpecificOutput").GetProperty("additionalContext").GetString();
        Assert.Contains("Recipes/Thing.cs compiles, but the build fails", context);
        Assert.Contains("Other.cs", context);
    }

    [Fact]
    public void Per_edit_skips_uncovered_missing_and_foreign_files_without_running_dotnet()
    {
        _box.WriteOther("Recipes/Foreign.cs", "public class Foreign { int z = BROKEN; }");

        Assert.Equal(0, _box.PerEdit(_box.Path("README.md")).Exit);
        Assert.Equal(0, _box.PerEdit(_box.Path("Recipes/Missing.cs")).Exit);
        Assert.Equal(0, _box.PerEdit(_box.OtherPath("Recipes/Foreign.cs")).Exit);
        Assert.Empty(_box.DotnetCalls());
    }

    [Theory]
    [InlineData("")]
    [InlineData("{}")]
    [InlineData("not json")]
    [InlineData("""{"tool_name":"Bash","tool_input":{"command":"ls"}}""")]
    [InlineData("""{"tool_name":"Edit","tool_input":{}}""")]
    public void Per_edit_ignores_payloads_without_a_path(string payload)
    {
        var r = _box.Run(HookSandbox.PerEditScript, payload);

        Assert.Equal(0, r.Exit);
        Assert.Equal("", r.Stderr);
        Assert.Empty(_box.DotnetCalls());
    }

    [Fact]
    public void Per_edit_resolves_a_relative_path_against_cwd()
    {
        _box.Write("Recipes/Thing.cs", "public class Thing { int x = BROKEN; }");

        var r = _box.PerEdit("Recipes/Thing.cs");

        Assert.Equal(2, r.Exit);
        Assert.Contains("Recipes/Thing.cs", r.Stderr);
    }

    [Fact]
    public void Per_edit_checks_the_edited_checkout_not_CLAUDE_PROJECT_DIR()
    {
        _box.WriteOther("Recipes/Thing.cs", "public class Thing { int x = BROKEN; }");

        var r = _box.PerEdit(_box.Path("Recipes/Thing.cs"), env: new() { ["CLAUDE_PROJECT_DIR"] = _box.OtherRoot });

        Assert.Equal(0, r.Exit);
        Assert.Equal("", r.Stderr);
    }

    [Fact]
    public void A_missing_dotnet_fails_visibly_instead_of_passing()
    {
        var missing = new Dictionary<string, string?> { ["HOOK_DOTNET"] = Path.Combine(_box.Root, "no-such-dotnet.exe") };
        _box.Write("Recipes/Thing.cs", "public class Thing { int changed; }");

        var perEdit = _box.PerEdit(_box.Path("Recipes/Thing.cs"), env: missing);
        var stop = _box.Stop(env: missing);

        Assert.Equal(2, perEdit.Exit);
        Assert.Contains("Build could not run", perEdit.Stderr);
        Assert.Equal(2, stop.Exit);
        Assert.Contains("Build could not run", stop.Stderr);
    }

    [Fact]
    public void Stop_blocks_on_an_error_written_without_the_per_edit_hook()
    {
        _box.Write("Recipes/Thing.cs", "public class Thing { int x = BROKEN; }");

        var r = _box.Stop();

        Assert.Equal(2, r.Exit);
        Assert.Contains("Fix these before you finish", r.Stderr);
        Assert.Contains("Thing.cs", r.Stderr);
        Assert.Contains("CS0103", r.Stderr);
    }

    [Theory]
    [InlineData("""{"stop_hook_active":true}""")]
    [InlineData("""{"loop_count":1}""")]
    public void Stop_after_one_retry_lets_the_agent_finish_while_the_error_is_present(string flags)
    {
        _box.Write("Recipes/Thing.cs", "public class Thing { int x = BROKEN; }");
        var payload = JsonSerializer.Serialize(new { cwd = _box.Root });
        payload = payload.TrimEnd('}') + "," + flags.TrimStart('{');

        var r = _box.Run(HookSandbox.StopScript, payload);

        Assert.Equal(0, r.Exit);
        Assert.Empty(_box.DotnetCalls());
    }

    [Fact]
    public void Stop_runs_nothing_when_no_build_input_changed()
    {
        Assert.Equal(0, _box.Stop().Exit);
        _box.Write("README.md", "docs only");
        Assert.Equal(0, _box.Stop().Exit);

        Assert.Empty(_box.DotnetCalls());
    }

    [Fact]
    public void Stop_reports_failing_tests_and_pending_model_changes_together()
    {
        _box.Write("KitchenAssistant.Tests/Recipes/ThingTests.cs", "public class ThingTests { /* FAILING_ASSERT */ }");
        _box.Write("Data/Product.cs", "public class Product { int PENDING_MODEL; }");

        var r = _box.Stop();

        Assert.Equal(2, r.Exit);
        Assert.Contains("Tests fail", r.Stderr);
        Assert.Contains("Pending model changes check fails", r.Stderr);
    }

    [Fact]
    public void Stop_does_not_repeat_a_sweep_that_was_already_green()
    {
        _box.Write("Recipes/Thing.cs", "public class Thing { int ok; }");

        Assert.Equal(0, _box.Stop().Exit);
        var callsAfterFirst = _box.DotnetCalls().Count;
        Assert.Equal(0, _box.Stop().Exit);

        Assert.True(callsAfterFirst > 0);
        Assert.Equal(callsAfterFirst, _box.DotnetCalls().Count);
    }

    [Fact]
    public void Stop_runs_the_hook_tests_only_when_the_hooks_changed()
    {
        _box.Write("Recipes/Thing.cs", "public class Thing { int ok; }");
        _box.Stop();
        Assert.Contains("--filter Category!=AgentHooks", Assert.Single(_box.DotnetCalls(), c => c.Contains("|test ")));

        _box.Write(".claude/hooks/lib.mjs", File.ReadAllText(_box.Path(".claude/hooks/lib.mjs")) + "\n// changed\n");
        _box.Stop();
        Assert.DoesNotContain("--filter", _box.DotnetCalls().Where(c => c.Contains("|test ")).Last());
    }

    [Fact]
    public void Stop_sweeps_a_sibling_worktree_the_per_edit_hook_registered()
    {
        var worktree = _box.AddWorktree();
        var file = System.IO.Path.Combine(worktree, "Recipes", "Thing.cs");
        File.WriteAllText(file, "public class Thing { int x = BROKEN; }");

        var perEdit = _box.PerEdit(file, session: "s-worktree");
        var stop = _box.Stop(session: "s-worktree");
        var otherSession = _box.Stop(session: "s-unrelated");

        Assert.Equal(2, perEdit.Exit);
        Assert.Equal(2, stop.Exit);
        Assert.Contains(System.IO.Path.GetFileName(worktree), stop.Stderr);
        Assert.Equal(0, otherSession.Exit);
    }

    [Fact]
    public void Settings_commands_reach_their_scripts_from_a_worktree_with_a_stale_CLAUDE_PROJECT_DIR()
    {
        var worktree = _box.AddWorktree();
        var file = System.IO.Path.Combine(worktree, "Recipes", "Thing.cs");
        File.WriteAllText(file, "public class Thing { int x = BROKEN; }");
        var commands = HookSandbox.SettingsCommands();
        Assert.Equal(2, commands.Count);

        foreach (var (eventName, command) in commands)
        {
            var payload = eventName == "Stop"
                ? JsonSerializer.Serialize(new { cwd = worktree, stop_hook_active = false })
                : JsonSerializer.Serialize(new { cwd = worktree, tool_name = "Edit", tool_input = new { file_path = file } });

            var r = _box.RunCommand(command, payload, worktree, new() { ["CLAUDE_PROJECT_DIR"] = System.IO.Path.Combine(_box.Root, "gone") });

            Assert.True(r.Exit == 2, $"{eventName}: exit {r.Exit}, stderr: {r.Stderr}");
            Assert.Contains("Thing.cs", r.Stderr);
        }
    }
}

internal sealed record HookResult(int Exit, string Stdout, string Stderr);

// A throwaway git repo shaped like this one (solution, test project, Recipes/, Data/), a second unrelated repo, an
// isolated temp dir for the hooks' state, and the dotnet stub.
internal sealed class HookSandbox : IDisposable
{
    public const string PerEditScript = "check-edited-file.mjs";
    public const string StopScript = "end-of-turn.mjs";

    private static readonly string RepoRoot = FindRepoRoot();
    private readonly string _base = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "agent-hooks-" + Guid.NewGuid().ToString("N")[..8]);
    private readonly string _tmp;
    private readonly string _log;
    private readonly string _stub;
    private int _worktrees;

    public HookSandbox()
    {
        Root = System.IO.Path.Combine(_base, "repo");
        OtherRoot = System.IO.Path.Combine(_base, "other");
        _tmp = System.IO.Path.Combine(_base, "tmp");
        _log = System.IO.Path.Combine(_base, "dotnet-calls.log");
        _stub = System.IO.Path.Combine(_base, "dotnet-stub.mjs");
        Directory.CreateDirectory(_tmp);
        File.WriteAllText(_stub, StubSource);
        File.WriteAllText(_log, "");

        Write("KitchenAssistant.slnx", "<Solution />");
        Write("KitchenAssistant.Tests/KitchenAssistant.Tests.csproj", "<Project />");
        Write("Recipes/Thing.cs", "public class Thing { }");
        Write("Recipes/Other.cs", "public class Other { }");
        Write("KitchenAssistant.Tests/Recipes/ThingTests.cs", "public class ThingTests { Thing t; }");
        Write("KitchenAssistant.Tests/Recipes/OtherTests.cs", "public class OtherTests { Other o; }");
        Write("Data/Product.cs", "public class Product { }");
        Write("README.md", "readme");
        foreach (var script in Directory.GetFiles(System.IO.Path.Combine(RepoRoot, ".claude", "hooks"), "*.mjs"))
        {
            Write(".claude/hooks/" + System.IO.Path.GetFileName(script), File.ReadAllText(script));
        }
        InitRepo(Root);

        Directory.CreateDirectory(OtherRoot);
        File.WriteAllText(System.IO.Path.Combine(OtherRoot, "README.md"), "other");
        InitRepo(OtherRoot);
    }

    public string Root { get; }

    public string OtherRoot { get; }

    public string Path(string relative) => System.IO.Path.Combine(Root, relative.Replace('/', System.IO.Path.DirectorySeparatorChar));

    public string OtherPath(string relative) => System.IO.Path.Combine(OtherRoot, relative.Replace('/', System.IO.Path.DirectorySeparatorChar));

    public void Write(string relative, string content)
    {
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path(relative))!);
        File.WriteAllText(Path(relative), content);
    }

    public void WriteOther(string relative, string content)
    {
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(OtherPath(relative))!);
        File.WriteAllText(OtherPath(relative), content);
    }

    public string AddWorktree()
    {
        var path = System.IO.Path.Combine(_base, $"wt{++_worktrees}");
        Git(Root, "worktree", "add", "--detach", path);
        return path;
    }

    // One line per stub call: "<cwd>|<args>".
    public List<string> DotnetCalls() => File.ReadAllLines(_log).Where(l => l.Length > 0).ToList();

    public HookResult PerEdit(string file, string? session = null, Dictionary<string, string?>? env = null) =>
        Run(PerEditScript, JsonSerializer.Serialize(new { session_id = session, cwd = Root, tool_name = "Edit", tool_input = new { file_path = file } }), env);

    public HookResult Stop(string? session = null, Dictionary<string, string?>? env = null) =>
        Run(StopScript, JsonSerializer.Serialize(new { session_id = session, cwd = Root, hook_event_name = "Stop", stop_hook_active = false }), env);

    public HookResult Run(string script, string payload, Dictionary<string, string?>? env = null) =>
        Exec("node", [System.IO.Path.Combine(RepoRoot, ".claude", "hooks", script)], payload, Root, env);

    // A settings.json command as the harness runs it: bash -c, from the given directory.
    public HookResult RunCommand(string command, string payload, string cwd, Dictionary<string, string?> env) =>
        Exec(Bash(), ["-c", command], payload, cwd, env);

    public static List<(string Event, string Command)> SettingsCommands()
    {
        using var json = JsonDocument.Parse(File.ReadAllText(System.IO.Path.Combine(RepoRoot, ".claude", "settings.json")));
        return json.RootElement.GetProperty("hooks").EnumerateObject()
            .SelectMany(e => e.Value.EnumerateArray().SelectMany(m => m.GetProperty("hooks").EnumerateArray())
                .Select(h => (e.Name, h.GetProperty("command").GetString()!)))
            .ToList();
    }

    public void Dispose()
    {
        try
        {
            foreach (var file in Directory.EnumerateFiles(_base, "*", SearchOption.AllDirectories))
            {
                File.SetAttributes(file, FileAttributes.Normal); // git objects are read-only on Windows
            }
            Directory.Delete(_base, recursive: true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
        }
    }

    private HookResult Exec(string fileName, string[] args, string stdin, string cwd, Dictionary<string, string?>? env)
    {
        var info = new ProcessStartInfo(fileName)
        {
            WorkingDirectory = cwd,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var arg in args)
        {
            info.ArgumentList.Add(arg);
        }
        info.Environment.Remove("CLAUDE_PROJECT_DIR");
        info.Environment["TEMP"] = _tmp;
        info.Environment["TMP"] = _tmp;
        info.Environment["TMPDIR"] = _tmp;
        info.Environment["HOOK_DOTNET"] = _stub;
        info.Environment["STUB_LOG"] = _log;
        foreach (var (name, value) in env ?? [])
        {
            info.Environment[name] = value;
        }

        using var process = Process.Start(info)!;
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        process.StandardInput.Write(stdin);
        process.StandardInput.Close();
        if (!process.WaitForExit(60_000))
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException($"{fileName} {string.Join(' ', args)} did not finish in 60 s");
        }
        return new HookResult(process.ExitCode, stdout.Result, stderr.Result);
    }

    private static void InitRepo(string dir)
    {
        Git(dir, "init", "-q");
        Git(dir, "add", "-A");
        Git(dir, "-c", "user.name=hooks", "-c", "user.email=hooks@test", "commit", "-q", "-m", "init");
    }

    private static string Git(string dir, params string[] args)
    {
        var info = new ProcessStartInfo("git") { WorkingDirectory = dir, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var arg in new[] { "-c", "core.autocrlf=false" }.Concat(args))
        {
            info.ArgumentList.Add(arg);
        }
        using var process = Process.Start(info)!;
        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        return process.ExitCode == 0 ? output.Trim() : throw new InvalidOperationException($"git {string.Join(' ', args)}: {error}");
    }

    // Git Bash on Windows (System32\bash.exe is WSL, not the shell Claude Code uses); bash elsewhere.
    private static string Bash()
    {
        if (!OperatingSystem.IsWindows())
        {
            return "bash";
        }
        var bash = System.IO.Path.GetFullPath(System.IO.Path.Combine(Git(RepoRoot, "--exec-path"), "..", "..", "..", "bin", "bash.exe"));
        return File.Exists(bash) ? bash : throw new FileNotFoundException("Git Bash not found next to git", bash);
    }

    // From the test binary, or from this source file: the hooks build into an output dir outside the repo.
    private static string FindRepoRoot([CallerFilePath] string sourceFile = "")
    {
        foreach (var start in new[] { AppContext.BaseDirectory, System.IO.Path.GetDirectoryName(sourceFile) ?? "" })
        {
            for (var dir = new DirectoryInfo(start); dir is not null; dir = dir.Parent)
            {
                if (File.Exists(System.IO.Path.Combine(dir.FullName, ".claude", "hooks", "end-of-turn.mjs")))
                {
                    return dir.FullName;
                }
            }
        }
        throw new DirectoryNotFoundException($"No .claude/hooks/end-of-turn.mjs above {AppContext.BaseDirectory} or {sourceFile}");
    }

    private const string StubSource = """
        // dotnet stand-in for the hook tests: build/test/ef results come from marker strings in the checkout.
        import fs from 'node:fs';
        import path from 'node:path';
        const args = process.argv.slice(2);
        fs.appendFileSync(process.env.STUB_LOG, `${process.cwd()}|${args.join(' ')}\n`);
        const walk = (d) => fs.readdirSync(d, { withFileTypes: true }).flatMap((e) =>
          e.name === '.git' || e.name === '.claude' ? [] : e.isDirectory() ? walk(path.join(d, e.name)) : [path.join(d, e.name)]);
        const files = walk(process.cwd());
        const containing = (re, marker) => files.filter((f) => re.test(f) && fs.readFileSync(f, 'utf8').includes(marker));
        if (args[0] === 'build') {
          const broken = containing(/\.(cs|razor)$/, 'BROKEN');
          for (const f of broken) console.log(`${f}(1,30): error CS0103: The name 'BROKEN' does not exist in the current context [${path.join(process.cwd(), 'App.csproj')}]`);
          process.exit(broken.length ? 1 : 0);
        }
        if (args[0] === 'test') {
          const i = args.indexOf('--filter');
          const filter = i >= 0 ? args[i + 1] : '';
          const tests = files.filter((f) => /Tests\.cs$/.test(f));
          const selected = filter.startsWith('FullyQualifiedName')
            ? tests.filter((f) => filter.split('|').includes(`FullyQualifiedName~${path.basename(f, '.cs')}`))
            : tests;
          const failing = selected.filter((f) => fs.readFileSync(f, 'utf8').includes('FAILING_ASSERT'));
          for (const f of failing) console.log(`  Failed KitchenAssistant.Tests.${path.basename(f, '.cs')}.Fails [1 ms]\n  Error Message:\n   Assert.Equal() Failure`);
          if (selected.length === 0) { console.log('No test matches the given testcase filter'); process.exit(0); }
          console.log(`Total: ${selected.length}`);
          process.exit(failing.length ? 1 : 0);
        }
        if (args[0] === 'ef') {
          const pending = containing(/[\\/]Data[\\/].*\.cs$/, 'PENDING_MODEL');
          console.log(pending.length ? 'Changes have been made to the model since the last migration.' : 'No changes have been made to the model since the last migration.');
          process.exit(pending.length ? 1 : 0);
        }
        console.log(`stub: unexpected dotnet ${args.join(' ')}`);
        process.exit(1);
        """;
}
