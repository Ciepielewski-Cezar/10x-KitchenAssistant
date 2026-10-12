// Shared helpers for the Claude Code hooks in this directory.
// Signal protocol (Claude Code): exit 2 + stderr reaches the agent; any other non-zero exit
// reaches nobody. Every failure below therefore ends in fail() (stderr + exit 2).
import { spawnSync } from 'node:child_process';
import crypto from 'node:crypto';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';

export const SOLUTION = 'KitchenAssistant.slnx';
export const TEST_PROJECT = 'KitchenAssistant.Tests/KitchenAssistant.Tests.csproj';
export const TEST_DIR = 'KitchenAssistant.Tests/';
export const HOOKS_DIR = '.claude/hooks/';
// The hook tests spawn node and git; the end-of-turn sweep runs them only when the hooks changed.
export const HOOK_TESTS_FILTER = 'Category!=AgentHooks';

// Files the compiler reports diagnostics for (per-edit check).
export const COVERED = /\.(cs|razor|cshtml)$/i;
// Files whose change can alter the build or test result (end-of-turn sweep).
export const BUILD_INPUTS =
  /(\.(cs|razor|cshtml|csproj|slnx|props|targets)$)|((^|\/)(appsettings[^/]*\.json|global\.json|dotnet-tools\.json|nuget\.config)$)/i;

// The build gate is "compiler + nullable warnings": warnings fail the build. NuGet audit
// advisories stay warnings; they belong to the vulnerable-packages CI job, not the agent loop.
export const BUILD_ARGS = [
  'build', SOLUTION, '-nologo', '-v:q', '-clp:NoSummary',
  '-p:TreatWarningsAsErrors=true', '-p:WarningsNotAsErrors=NU1901%3BNU1902%3BNU1903%3BNU1904',
];

export const HOOK_TMP = path.join(os.tmpdir(), 'claude-hooks');

export function fail(message) {
  process.stderr.write(message.endsWith('\n') ? message : message + '\n');
  process.exit(2);
}

// Non-blocking context for the agent (PostToolUse: shown next to the tool result).
export function note(event, message) {
  process.stdout.write(JSON.stringify({ hookSpecificOutput: { hookEventName: event, additionalContext: message } }));
  process.exit(0);
}

// The payload is untrusted: empty stdin, invalid JSON or a non-object mean "nothing to check".
export function readPayload() {
  try {
    const value = JSON.parse(fs.readFileSync(0, 'utf8'));
    return value && typeof value === 'object' ? value : {};
  } catch {
    return {};
  }
}

export function str(value) {
  return typeof value === 'string' && value.length > 0 ? value : null;
}

export function sessionId(payload) {
  return (str(payload.session_id) ?? '').replace(/[^A-Za-z0-9_-]/g, '') || null;
}

export function git(cwd, args) {
  const r = spawnSync('git', ['-C', cwd, ...args], { encoding: 'utf8', stdio: ['ignore', 'pipe', 'pipe'] });
  return r.status === 0 ? r.stdout.trim() : null;
}

export function lines(text) {
  return (text ?? '').split(/\r?\n/).map((l) => l.trim()).filter(Boolean);
}

// Canonical path: on Windows git and the payload can name one directory as an 8.3 short path
// (C:/Users/ABCDEF~1) and as the long path, so expand it before comparing or relativising.
export function real(p) {
  try {
    return fs.realpathSync.native(p);
  } catch {
    return path.resolve(p);
  }
}

// Comparable form of a path: canonical, forward slashes, case-folded on Windows.
export function key(p) {
  const n = real(p).replace(/\\/g, '/');
  return process.platform === 'win32' ? n.toLowerCase() : n;
}

export function toplevel(dir) {
  const top = git(dir, ['rev-parse', '--show-toplevel']);
  return top ? real(top) : null;
}

export function commonDir(dir) {
  const d = git(dir, ['rev-parse', '--path-format=absolute', '--git-common-dir']);
  return d ? key(d) : null;
}

function rootId(root) {
  return crypto.createHash('sha1').update(key(root)).digest('hex').slice(0, 12);
}

// Checkouts the per-edit hook saw this session, so the Stop hook sweeps sibling worktrees too.
export function registryFile(sid) {
  return path.join(HOOK_TMP, 'sessions', `${sid}.roots`);
}

export function register(sid, root) {
  if (!sid) return;
  fs.mkdirSync(path.dirname(registryFile(sid)), { recursive: true });
  fs.appendFileSync(registryFile(sid), root + '\n');
}

export function greenFile(root) {
  return path.join(HOOK_TMP, 'green', rootId(root));
}

// dotnet with the hooks' own output dir (UseArtifactsOutput): a running app, `dotnet watch` or
// the agent's own `dotnet build` keep bin/ and obj/, so hook builds never hit a locked DLL.
// HOOK_DOTNET replaces the binary in the hook tests (a .mjs runs under this node).
export function dotnet(root, args, timeoutSeconds) {
  const stub = process.env.HOOK_DOTNET;
  const [cmd, pre] = stub ? (/\.m?js$/i.test(stub) ? [process.execPath, [stub]] : [stub, []]) : ['dotnet', []];
  const r = spawnSync(cmd, [...pre, ...args], {
    cwd: root,
    encoding: 'utf8',
    stdio: ['ignore', 'pipe', 'pipe'],
    timeout: timeoutSeconds * 1000,
    maxBuffer: 64 * 1024 * 1024,
    env: {
      ...process.env,
      NO_COLOR: '1',
      FORCE_COLOR: '0',
      DOTNET_NOLOGO: '1',
      DOTNET_CLI_TELEMETRY_OPTOUT: '1',
      MSBUILDTERMINALLOGGER: 'off',
      UseArtifactsOutput: 'true',
      ArtifactsPath: path.join(HOOK_TMP, 'build', rootId(root)),
    },
  });
  const out = `${r.stdout ?? ''}${r.stderr ?? ''}`;
  if (r.error) {
    const why = r.error.code === 'ETIMEDOUT' ? `timed out after ${timeoutSeconds}s` : r.error.message;
    return { ok: false, ran: false, out: `dotnet ${args[0]} did not run: ${why}\n${out}` };
  }
  return { ok: r.status === 0, ran: true, out };
}

// MSBuild diagnostics: `C:\path\File.cs(12,5): error CS0103: ... [C:\path\Project.csproj]`.
const DIAGNOSTIC = /^(.+?)\((\d+),(\d+)(?:,\d+,\d+)?\): (error|warning) ([A-Z]+\d+): (.*?)(?: \[[^\]]+\])?$/;

export function diagnostics(output) {
  const seen = new Set();
  const result = [];
  for (const line of lines(output)) {
    const m = DIAGNOSTIC.exec(line);
    if (!m || seen.has(line)) continue;
    seen.add(line);
    result.push({ file: m[1], text: `${m[1]}(${m[2]},${m[3]}): ${m[4]} ${m[5]}: ${m[6]}` });
  }
  return result;
}

// Number of tests `dotnet test` executed, or null when no summary line was printed.
export function testsRun(output) {
  const m = /Total:\s+(\d+)/.exec(output);
  return m ? Number(m[1]) : /No test matches the given testcase filter/i.test(output) ? 0 : null;
}

// Non-empty lines, capped. Framework stack frames (`at System.…`, `at Xunit.…`) are dropped:
// the test's own frame names the failing line.
export function clip(text, max = 150) {
  const all = (text ?? '')
    .split(/\r?\n/)
    .filter((l) => l.trim() && !/^\s*(at (System|Xunit|Microsoft)\.|--- End of stack trace)/.test(l));
  return all.length <= max ? all.join('\n') : `${all.slice(0, max).join('\n')}\n... (${all.length - max} more lines)`;
}

// One build per checkout at a time: parallel edits fire parallel hooks on the same output dir.
export function withLock(root, fn) {
  const dir = path.join(HOOK_TMP, 'locks', rootId(root));
  fs.mkdirSync(path.dirname(dir), { recursive: true });
  const deadline = Date.now() + 240_000;
  for (;;) {
    try {
      fs.mkdirSync(dir);
      break;
    } catch (e) {
      if (e.code !== 'EEXIST') throw e;
      try {
        if (Date.now() - fs.statSync(dir).mtimeMs > 600_000) fs.rmSync(dir, { recursive: true, force: true });
      } catch {}
      if (Date.now() > deadline) fail(`Hook build lock ${dir} held for 4 minutes; remove it if no hook is running.`);
      Atomics.wait(new Int32Array(new SharedArrayBuffer(4)), 0, 0, 250);
    }
  }
  // fail() and note() end the process inside fn, which skips `finally`: release on exit too.
  const release = () => fs.rmSync(dir, { recursive: true, force: true });
  process.on('exit', release);
  try {
    return fn();
  } finally {
    release();
    process.off('exit', release);
  }
}
