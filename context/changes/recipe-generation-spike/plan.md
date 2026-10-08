# Recipe generation spike (S-07) Implementation Plan

## Overview

S-02 shipped AI recipe generation with the defaults `claude-sonnet-5-5` / effort `low`. Those defaults rest on a single production-like smoke-test call (14.5 s, 5 proposals, 20/20 valid product IDs). This change measures at least 10 real calls on a realistic pantry, records latency, tokens, quality and product-ID validity in `spike.md`, and commits the measured `Recipes` defaults. It answers the roadmap question: do 5 full recipes in one call fit in a minute, or must generation be split into a list and then the full recipe? The answer also decides whether S-04 needs a second AI call.

Moved here from S-02's Phase 3 on 2026-10-08. The contract is unchanged, and the runbook, pantry and measurement tables are already in `spike.md`.

## What We're NOT Doing

- No new code unless a lever below needs it (switching to `anyOf` in the schema, or a terser prompt). Measurement uses the S-02 log lines as they are.
- No real-API test in the automated suite.
- No list/details split. If the levers fail, stop and re-plan it together with S-04.

## Phase 1: Measure and lock the defaults

### Overview

The only change that spends money: about 10–20 calls, roughly $0.025 each (from the smoke test), so well under $2.

### Changes Required:

#### 1. Dev key and switch (user)

**Intent**: Enable real calls without exposing the key.

**Contract**: You run `dotnet user-secrets set ANTHROPIC_API_KEY <dev-key>` yourself (never in chat), with a low monthly spend limit on the dev workspace in the Console. Set `Recipes:Generator = Anthropic` via user-secrets or a local override; do not commit the Development switch. Disconnect from the VPN if TLS inspection breaks calls (`context/changes/local-dev/local-dev-plan.md:99`).

#### 2. Spike log

**File**: `context/changes/recipe-generation-spike/spike.md`

**Intent**: Record evidence that decides the defaults and S-04's shape.

**Contract**: Use a realistic pantry of about 20–30 Polish products. Make ≥ 10 calls at Sonnet 5.5 / low, not counting the first, schema-compiling call. Give each call a row with:
- elapsed seconds and stop reason;
- input, output and thinking tokens;
- recipe count;
- owned-by-ID, owned-by-name-fallback and unknown-ID counts;
- a quality note (Polish, logical steps).

The summary gives median and max latency, the estimated cost per request, and a verdict.

#### 3. Decide and lock

**Files**: `appsettings.json`, `context/foundation/roadmap.md` (S-07 Unknowns)

**Intent**: Commit the measured choice.

**Contract**: If every measured call finishes within 60 s, keep (or adjust) the `Recipes:Model`/`Effort` defaults and record the answer in the roadmap's S-07 Unknowns. If not, apply the levers in order, re-measuring ≥ 5 calls each:
1. Haiku 4.5;
2. terser recipes (prompt);
3. stop and re-plan the list/details split with S-04.

If the API rejects the nullable `productId` schema, switch it to `anyOf` and re-run.

### Success Criteria:

#### Automated Verification:

- All tests still pass after the default changes: `dotnet test KitchenAssistant.slnx`

#### Manual Verification:

- Dev key set via user-secrets with a Console spend limit; the startup missing-key warning is gone
- At least 10 real calls on a realistic pantry each finish within 60 s, with latency, tokens and stop reason recorded in `spike.md`
- Recipes are in Polish with logical steps, and the ID validity rate is recorded in `spike.md`
- The chosen model/effort is committed as the `Recipes` defaults in `appsettings.json` and the roadmap S-07 unknown is answered (or the split is escalated to re-planning)

**Implementation Note**: This phase is manual-heavy and needs the user's key. The agent reads the log lines and fills in `spike.md`; the user runs the calls.

## References

- Origin: `context/changes/first-recipe-generation/plan.md` (Phase 3, scope change 2026-10-08)
- Runbook, pantry and tables: `spike.md`
- Log lines the spike reads: `Recipes/AnthropicRecipeGenerator.cs`, `Recipes/RecipeService.cs`
- Roadmap: `context/foundation/roadmap.md` (S-07; S-04 depends on the verdict)

## Progress

> Convention: `- [ ]` pending, `- [x]` done. Append ` — <commit sha>` when a step lands. Do not rename step titles. See `references/progress-format.md`.

### Phase 1: Measure and lock the defaults

#### Automated

- [ ] 1.1 All tests still pass after the default changes: `dotnet test KitchenAssistant.slnx`

#### Manual

- [ ] 1.2 Dev key set via user-secrets with a Console spend limit; the startup missing-key warning is gone
- [ ] 1.3 At least 10 real calls on a realistic pantry each finish within 60 s, with latency, tokens and stop reason recorded in `spike.md`
- [ ] 1.4 Recipes are in Polish with logical steps, and the ID validity rate is recorded in `spike.md`
- [ ] 1.5 The chosen model/effort is committed as the `Recipes` defaults in `appsettings.json` and the roadmap S-07 unknown is answered (or the split is escalated to re-planning)
