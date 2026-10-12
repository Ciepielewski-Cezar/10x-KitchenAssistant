// Stop: sweep everything this turn changed before the agent finishes, one retry.
// Build (warnings as errors), the whole unit suite, and the pending-model-changes check when
// Data/ changed. Also covers files rewritten through Bash, which never reach the per-edit hook.
import crypto from 'node:crypto';
import fs from 'node:fs';
import path from 'node:path';
import {
  BUILD_ARGS, BUILD_INPUTS, HOOKS_DIR, HOOK_TESTS_FILTER, TEST_PROJECT, clip, diagnostics, dotnet,
  greenFile, key, lines, readPayload, registryFile, sessionId, str, testsRun, toplevel, withLock, git,
} from './lib.mjs';

const payload = readPayload();

// Already sent back once by this hook: let it finish. The commit gate catches the rest.
// (loop_count covers Cursor, which imports hooks from .claude/settings.json.)
if (payload.stop_hook_active === true || Number(payload.loop_count ?? 0) > 0) process.exit(0);

// Checkouts to sweep: the session's (payload cwd follows EnterWorktree, $CLAUDE_PROJECT_DIR does
// not) plus every checkout check-edited-file.mjs registered for this session.
const cwd = str(payload.cwd) ?? process.cwd();
const sid = sessionId(payload);
const candidates = [toplevel(cwd)];
if (sid && fs.existsSync(registryFile(sid))) candidates.push(...lines(fs.readFileSync(registryFile(sid), 'utf8')));
const roots = [...new Map(candidates.filter((r) => r && fs.existsSync(r)).map((r) => [key(r), r])).values()];
if (roots.length === 0) {
  // Not silent: the user sees why nothing was checked.
  process.stdout.write(JSON.stringify({ systemMessage: `end-of-turn.mjs: no git checkout at ${cwd}, nothing checked.` }));
  process.exit(0);
}

let report = '';
const green = [];
for (const root of roots) {
  // Changed and new files. None that feed the build (a Q&A turn, docs only): nothing to check.
  const changed = [
    ...new Set([...lines(git(root, ['diff', '--name-only', 'HEAD'])), ...lines(git(root, ['ls-files', '-o', '--exclude-standard']))]),
  ].sort();
  const relevant = changed.filter((f) => BUILD_INPUTS.test(f) || f.startsWith(HOOKS_DIR));
  if (relevant.length === 0) continue;

  // Same content as the last green sweep of this checkout: already checked.
  const hash = crypto.createHash('sha1').update(git(root, ['rev-parse', 'HEAD']) ?? '');
  for (const f of relevant) {
    const abs = path.join(root, f);
    hash.update(`\0${f}\0`).update(fs.existsSync(abs) && fs.statSync(abs).isFile() ? fs.readFileSync(abs) : '<deleted>');
  }
  const fingerprint = hash.digest('hex');
  const marker = greenFile(root);
  if (fs.existsSync(marker) && fs.readFileSync(marker, 'utf8') === fingerprint) continue;

  const problems = withLock(root, () => {
    const found = [];
    const build = dotnet(root, BUILD_ARGS, 150);
    if (!build.ok) {
      const diags = diagnostics(build.out);
      found.push(
        diags.length > 0
          ? `Build fails (warnings are errors here):\n${clip(diags.map((d) => d.text).join('\n'), 60)}`
          : `Build could not run:\n${clip(build.out, 40)}`,
      );
      return found; // Tests and the model check need a build.
    }

    // The whole suite takes seconds. The hook tests only when the hooks changed.
    const hooksChanged = relevant.some((f) => f.startsWith(HOOKS_DIR));
    const testArgs = ['test', TEST_PROJECT, '--no-build', '-nologo', ...(hooksChanged ? [] : ['--filter', HOOK_TESTS_FILTER])];
    const tests = dotnet(root, testArgs, 120);
    if (!tests.ok) found.push(`Tests fail:\n${clip(tests.out)}`);
    else if (!testsRun(tests.out)) found.push(`dotnet test ran no tests:\n${clip(tests.out, 20)}`);

    // A model change without a migration stops startup (Database:MigrateOnStartup).
    if (relevant.some((f) => f.startsWith('Data/'))) {
      const ef = dotnet(root, ['ef', 'migrations', 'has-pending-model-changes', '--no-build'], 90);
      if (!ef.ok) {
        found.push(
          `Pending model changes check fails (add a migration: dotnet ef migrations add <Name> --output-dir Data/Migrations):\n${clip(ef.out, 20)}`,
        );
      }
    }
    return found;
  });

  if (problems.length > 0) report += `\n[${root}]\n${problems.join('\n\n')}\n`;
  else green.push([marker, fingerprint]);
}

for (const [marker, fingerprint] of green) {
  fs.mkdirSync(path.dirname(marker), { recursive: true });
  fs.writeFileSync(marker, fingerprint);
}
if (report) {
  process.stderr.write(`Fix these before you finish:${report}`);
  process.exit(2);
}
// Green: start the next turn with an empty registry.
if (sid && fs.existsSync(registryFile(sid))) fs.writeFileSync(registryFile(sid), '');
process.exit(0);
