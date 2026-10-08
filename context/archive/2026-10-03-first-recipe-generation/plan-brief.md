# First recipe generation (S-02) — Plan Brief

> Full plan: `context/changes/first-recipe-generation/plan.md`
> Research: `context/changes/first-recipe-generation/research.md`
> Scope change 2026-10-08: the measured spike (Phase 3 rows 3.1–3.5) and `spike.md` moved to `context/changes/recipe-generation-spike/` (roadmap S-07). S-02's Phase 3 is now only the smoke test.

## What & Why

A signed-in user asks for recipes and, within a minute, gets up to 5 AI-generated proposals built from their own products, in Polish, with ingredients and steps. This is the roadmap's north star: it tests the riskiest product assumption, which is AI quality and speed. Ownership of each ingredient is decided in C# from product IDs, never taken from the AI.

## Starting Point

`ProductService.GetProductsAsync` already returns each user's products with stable `int` IDs. `AnthropicClient` is registered (60 s timeout, SDK default of 2 retries, ~3 min worst case), but nothing calls it. There is no recipe code, page or test, and the dev API key is not set yet.

## Desired End State

„Przepisy” (`/recipes`) shows up to 5 proposal cards. Each ingredient is badged Masz, Zawsze w domu or Brakuje. Any failure shows a Polish error and „Spróbuj ponownie” within ~60 s. Development runs on a fake generator at zero cost. Production always uses Anthropic, and a measured spike (`spike.md`) has fixed the model/effort defaults.

## Key Decisions Made

| Decision | Choice | Why (1 sentence) | Source |
| --- | --- | --- | --- |
| Provider | Anthropic only, no multi-provider layer | Free tiers don't fit production in the EEA, and a switch adds scope | Research |
| Dev approach | Generator behind an interface; fake for UI/tests; real calls last | Zero API spend until the measured gate | Research |
| S-02 vs S-03 | Classify ingredients (owned / always-at-home / missing) and cap at 5; **no** >2 filter, score or sort | Keeps the roadmap slice boundaries; S-03 counts a ready classification | Plan |
| Unknown product ID | Ingredient becomes Brakuje under the AI's name; owned ones show the user's DB name | Exactly the PRD rule, and the recipe text stays coherent | Plan |
| Null ID, owned name | Exact pl-PL name match against the user's products → Masz (ID → name → always-at-home → missing) | A dropped ID must not show an owned product as missing; still decided in C# | Plan review F1 |
| Fake vs real | `Recipes:Generator` = `Fake` (Development) / `Anthropic` (default); fake refuses non-Development | Zero-cost dev, and production can never serve fake recipes | Plan |
| Prompt language | English instructions + "write everything in Polish" | Readable next to the code; checked in the spike | Plan |
| Model | Sonnet 5.5, effort `low`, one call for ≤ 5 full recipes, set in config | Mid price, good Polish; the spike can switch it without code | Plan |
| Failures | `MaxRetries = 1` inside one 60 s deadline per click; error + retry button | Survives brief overloads, never breaks the 1-minute guardrail | Plan |
| Always-at-home | sól, olej, woda + a fixed list of 22 dried spices; exact name match ignoring case; owned wins; PRD amended | Simple for the user, still deterministic and computed in C#; the prompt is built from the same list | Plan |
| Meal parameters | PRD defaults (obiad, ≤ 30 min, 1 porcja) passed to the prompt, no UI | S-05 only adds the controls | Plan |

## Scope

**In scope:** `Recipes/` feature (models, options, schema, prompt, parser, classifier, service, fake and Anthropic generators), the `/recipes` page and nav link, config switch with a production guard, unit tests, and a live spike plus smoke test.

**Out of scope:** score/sort/>2-missing filter (S-03), details view and persistence (S-04), meal-parameter UI (S-05), streaming, list/details split (only if the spike fails), DB changes, fuzzy always-at-home matching, real-API tests in CI.

## Architecture / Approach

`RecipeSuggestions.razor` → `RecipeService.GenerateAsync(userId)`, which loads the user's products, calls `IRecipeGenerator.GenerateJsonAsync` under a 60 s deadline, then parses (`RecipeResponseParser`) and classifies (`RecipeClassifier`) the result against the user's `ProductList`. `FakeRecipeGenerator` and `AnthropicRecipeGenerator` both return raw schema-shaped JSON, so one parse/classify path serves both. The Anthropic generator uses structured output (`OutputConfig.Format`) and treats any stop reason other than `end_turn` as a failure.

## Phases at a Glance

| Phase | What it delivers | Key risk |
| --- | --- | --- |
| 1. Recipe core with the fake generator | Types, schema, prompt, parser, classifier, fake, service + deadline, unit tests | Prompt wording only checked by hand until Phase 3 |
| 2. Anthropic generator and the /recipes page | Real structured-output call, DI switch + guard, page, nav; walkthrough on the fake | Response handling tested offline only |
| 3. Live gate — spike and smoke test | ≥ 10 measured real calls in `spike.md`, locked defaults, container smoke test | 5 full recipes may not fit in 60 s, forcing the list/details split |

**Prerequisites:** S-01 done (it is); before Phase 3, the user sets the dev key in user-secrets with a low Console spend limit.
**Estimated effort:** ~3 after-hours sessions (one per phase); Phase 3 costs well under $2.

## Open Risks & Assumptions

- Latency is unmeasured. If 5 full recipes in one call exceed 60 s even after switching to Haiku 4.5 and terser recipes, S-02/S-04 must be re-planned around a list/details split.
- The nullable `productId` (`["integer","null"]`) can only be confirmed against the live API; the fallback is `anyOf`.
- Until S-03 lands, users can see proposals with 3+ missing ingredients.
- A spice the AI names differently from the list (e.g. „pieprz czarny mielony”) or one not on the list shows as Brakuje; the spike checks how often this happens.
- Corporate TLS inspection can break real calls; run off VPN (`local-dev-plan.md:99`).

## Success Criteria (Summary)

- A user with products gets up to 5 Polish proposals with correctly badged ingredients, and never sees another user's products.
- No click waits more than ~60 s; failures show a clear Polish message with a retry.
- Real-API latency, cost and ID validity are measured and recorded, and the defaults reflect them.
