# Mutation report: RecipeService and RecipeResponseParser

Selective gate, not a coverage target (CLAUDE.md, Lesson 2). One Stryker.NET run after the risk phase, 2026-10-11.

## Run

```bash
cd KitchenAssistant.Tests
dotnet dotnet-stryker --mutate "**/Recipes/RecipeService.cs" --mutate "**/Recipes/RecipeResponseParser.cs" --reporter json --reporter cleartext
```

- Tool: `dotnet-stryker` 5.0.0 (local tool, `dotnet-tools.json`). It builds against net10.0 and finds `../KitchenAssistant.csproj` on its own, so no `stryker-config.json` is needed.
- 253 tests found. Run time about 1.5 minutes. Output goes to `KitchenAssistant.Tests/StrykerOutput/` (git-ignored).
- Stryker tried `--ignore-methods` on the command line to drop `Log*` calls. v5 rejects it as an unrecognised option (it is config-file only), so the log mutants stayed in the score.

## Score

| File | Mutants created | Compile error / ignored | Tested | Killed | Survived | Score before | Score after |
|---|---|---|---|---|---|---|---|
| `Recipes/RecipeService.cs` | 57 | 29 | 28 | 6 | 22 | 21.43 % | 21.43 % (no assertions added) |
| `Recipes/RecipeResponseParser.cs` | 6 | 6 (all compile error) | 0 | 0 | 0 | n/a | n/a |
| Total (reported by Stryker) | | | 28 | 6 | 22 | 21.43 % | 21.43 % |

Excluding the 21 survivors that only change a log call or a guard around one (all but the line-27 exception message), 6 of 6 behaviour-changing mutants were killed. That is the signal; the raw 21.43 % is an artefact of logging through `NullLogger`.

Survivors killed with a new assertion: 0. Survivors ignored on purpose: 22. No test files were changed.

## Survivors and verdicts

Question for each: would this change hurt a user or the business?

| Line | Mutant | Verdict | Reason |
|---|---|---|---|
| 27 | Message string of the `ArgumentOutOfRangeException` replaced by `""` | Ignore | Cosmetic. The exception type and `ParamName` are asserted (`A_disallowed_meal_parameter_throws_without_calling_the_generator`); the message is for developers. |
| 40 | `sent.Count <= all.Count` | Ignore | The `if` only guards one log line ("Sending N of M products"). `sent` is computed before it, so behaviour is unchanged. |
| 40 | `!(sent.Count < all.Count)` | Ignore | Same guard, log only. |
| 42 | Log statement removed | Ignore | Logging only (NullLogger). |
| 58 | Classification log statement removed | Ignore | Logging only. |
| 59 | Classification log message set to `""` | Ignore | Logging only. |
| 64 | `p.PrepTimeMinutes is not null` in the log argument | Ignore | Only changes the "usable without a preparation time" log count. |
| 74 | `stats.UnknownIds < 0` | Ignore | Guard for the unknown-ID warning only. |
| 74 | `stats.UnknownIds >= 0` | Ignore | Same, log only. |
| 74 | `!(stats.UnknownIds > 0)` | Ignore | Same, log only. |
| 76 | Unknown-ID warning removed | Ignore | Logging only. |
| 76 | Unknown-ID warning message set to `""` | Ignore | Logging only. |
| 82 | "none of them usable" error log removed | Ignore | Logging only. The `return Failed` after it is covered (`Malformed_or_only_invalid_recipes_is_Failed`, `Only_always_at_home_recipes_are_Failed_not_hidden`). |
| 102 | `!(deadline.IsCancellationRequested)` | Ignore | Only picks between two log messages (timed out vs cancelled by the client). Both branches return the same `Failed`. |
| 104 | Deadline error log removed | Ignore | Logging only. |
| 104 | Deadline error message set to `""` | Ignore | Logging only. |
| 108 | Per-attempt-timeout error log removed | Ignore | Logging only. |
| 108 | Per-attempt-timeout message set to `""` | Ignore | Logging only. |
| 115 | `RecipeGenerationException` error log removed | Ignore | Logging only. The mapping to `Failed` is covered (`Generator_failure_is_Failed`). |
| 115 | Message set to `""` | Ignore | Logging only. |
| 120 | `AnthropicException` error log removed | Ignore | Logging only. The mapping to `Failed` is covered. |
| 120 | Message set to `""` | Ignore | Logging only. |

Line numbers are the mutant's start line as Stryker reports it; the pairs above share a statement. The count by Stryker is 22 survivors (ids 852, 858, 859, 863, 868, 869, 871 to 874, 876, 877, 881, 892, 894, 895, 897, 898, 900, 901, 903, 904).

## Notes

- **Parser has no testable mutants.** Stryker's safe mode removed every mutant in `Parse` after a definite-assignment compile error (`response` is assigned inside the `try`). So the parser's strength is not measured by this run. Its behaviour is covered by `RecipeResponseParserTests` (14 cases: valid, empty array, malformed, wrong-typed, `null`, `{}`, `recipes: null`). A manual mutation of `Parse` (e.g. dropping the `?? throw`) would show whether that is enough; it needs a production edit, so it was not done here.
- **Killed mutants (6)** are the ones that matter for users: the `meal.IsAllowed` guard (line 25) and its throw (27), the empty-products short-circuit (33), the `MaxProducts` cap (`Take` to `Skip`, 39), the `ranked.Proposals.Count == 0` block (87), and the caller-cancellation filter `when (!ct.IsCancellationRequested)` (100).
- **Next time:** to get a score without log noise, add a `stryker-config.json` with `"ignore-methods": ["*.LogInformation", "*.LogWarning", "*.LogError"]` to `KitchenAssistant.Tests/`. Not added now, because the plan only asked for the CLI command.
