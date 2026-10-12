// PostToolUse (Write|Edit): compile, report the edited file's diagnostics, run its related tests.
// C# has no single-file check, so the solution is built (incrementally, in the hooks' own output
// dir) and only the edited file's diagnostics are reported; other files' errors are the
// end-of-turn hook's job. Related tests: the test classes whose source names the edited type.
import fs from 'node:fs';
import path from 'node:path';
import {
  BUILD_ARGS, COVERED, TEST_DIR, TEST_PROJECT, clip, commonDir, diagnostics, dotnet, fail, git,
  key, lines, note, readPayload, real, register, sessionId, str, testsRun, toplevel, withLock,
} from './lib.mjs';

const payload = readPayload();
const input = payload.tool_input && typeof payload.tool_input === 'object' ? payload.tool_input : {};
const cwd = str(payload.cwd) ?? process.cwd();
let file = str(input.file_path) ?? str(input.notebook_path) ?? str(payload.file_path);
if (!file) process.exit(0);
file = path.resolve(cwd, file);
if (!COVERED.test(file) || !fs.existsSync(file)) process.exit(0);
file = real(file);

// Checkout of the edited file, not $CLAUDE_PROJECT_DIR (stale after EnterWorktree). Files of
// other repositories are skipped; any worktree of this repository is checked.
const root = toplevel(path.dirname(file));
if (!root) process.exit(0);
const repo = commonDir(root);
if (!repo || repo !== commonDir(cwd)) process.exit(0);
register(sessionId(payload), root);

const rel = path.relative(root, file).replace(/\\/g, '/');
const typeName = path.basename(rel).replace(/(\.razor)?\.cs$|\.razor$|\.cshtml$/i, '');

function relatedTestClasses() {
  if (rel.startsWith(TEST_DIR) && /Tests\.cs$/i.test(rel)) return [typeName];
  // Test sources that name the type as a word. One level: a test that reaches the type
  // through another type is left to the end-of-turn sweep, which runs the whole suite.
  const hits = git(root, ['grep', '--untracked', '-l', '-w', '-F', '-e', typeName, '--', `${TEST_DIR}*.cs`]);
  return [...new Set(lines(hits).filter((f) => /Tests\.cs$/i.test(f)).map((f) => path.basename(f, '.cs')))];
}

withLock(root, () => {
  const build = dotnet(root, BUILD_ARGS, 150);
  if (!build.ok) {
    const all = diagnostics(build.out);
    const mine = all.filter((d) => key(path.resolve(root, d.file)) === key(file));
    if (mine.length > 0) {
      fail(`Build errors in ${rel} (warnings are errors here):\n${mine.map((d) => d.text).join('\n')}`);
    }
    if (all.length === 0) {
      fail(`Build could not run after editing ${rel}:\n${clip(build.out, 40)}`);
    }
    note(
      'PostToolUse',
      `${rel} compiles, but the build fails in ${all.length} diagnostic(s) elsewhere, so its related tests did not run. ` +
        `The end-of-turn check reports them. First: ${all[0].text}`,
    );
  }

  const classes = relatedTestClasses();
  if (classes.length === 0) return;
  const filter = classes.map((c) => `FullyQualifiedName~${c}`).join('|');
  const tests = dotnet(root, ['test', TEST_PROJECT, '--no-build', '-nologo', '--filter', filter], 120);
  if (!tests.ok) {
    fail(`Tests related to ${rel} fail (${classes.join(', ')}):\n${clip(tests.out)}`);
  }
  if (!testsRun(tests.out)) {
    note('PostToolUse', `No test ran for ${rel}: the filter ${filter} matched nothing.`);
  }
});
process.exit(0);
