---
date: 2026-10-03T23:45:33+02:00
researcher: Cezar Ciepielewski (with Claude Code)
git_commit: dd285f684941d84c98852719cba50aed7be3df63
branch: pantry-add-products-plan
repository: Ciepielewski-Cezar/10x-KitchenAssistant
topic: "Is an LLM API a good fit for the recipe generator (S-02), for a developer with no LLM API experience?"
tags: [research, ai, llm, anthropic, structured-output, recipe-generation, fr-005]
status: complete
last_updated: 2026-10-04
last_updated_by: Cezar Ciepielewski (with Claude Code)
last_updated_note: "Follow-ups: Max subscription vs API billing; free LLM alternatives; user chose the zero-cost development path"
---

# Research: Is an LLM a good fit for the recipe generator?

**Date**: 2026-10-03T23:45:33+02:00
**Researcher**: Cezar Ciepielewski (with Claude Code)
**Git Commit**: dd285f684941d84c98852719cba50aed7be3df63 (local working tree; `context/changes/first-recipe-generation/` is untracked)
**Branch**: pantry-add-products-plan
**Repository**: Ciepielewski-Cezar/10x-KitchenAssistant

## Research Question

"I don't have any experience with using LLM API. Is it a good idea to use it as a recipe generator, or I should change the idea?"

## Summary

**Keep the idea.** An LLM fits this job well, and the project is already shaped to keep its risks small:

1. **The task fits what LLMs do well.** The input is a short, free-form list of product names in Polish. The output is a few recipes with a fixed shape. A recipe database would have to match arbitrary user product names against its own ingredient vocabulary. The PRD rules that option out explicitly: recipes are "generowane przez AI … a nie wyszukiwane w gotowej bazie" (`context/foundation/prd.md:67`).
2. **The hard rules don't depend on the LLM.** The app computes the score, the missing-ingredient count and the order itself (`prd.md:81`, `CLAUDE.md` hard rules). It treats an ingredient as owned only if the returned product ID exists in that user's list (`prd.md:89`), and it rejects proposals with more than 2 missing ingredients (`prd.md:93`). The LLM writes recipe text and points at product IDs; plain C# code checks those IDs. The checks can be unit-tested without calling the AI.
3. **The integration is one HTTP call.** It is not an agent. The official `Anthropic` C# SDK 12.50.0 is already referenced (`KitchenAssistant.csproj:16`), and `AnthropicClient` is registered as a singleton with a 60 s timeout (`Program.cs:58-64`). Structured output (`OutputConfig.Format = new JsonOutputFormat { Schema = … }`) forces the response into your JSON schema, which `CLAUDE.md` requires for FR-005.
4. **The real risks are latency and quality, not feasibility.** Neither can be settled from documents. Latency is whether 5 full recipes arrive within 1 minute (`roadmap.md:112-114`). Quality is whether the recipes make sense and use the right IDs. Both need a short measured spike with a real key. That spike is the first open question below.

What is **not** ready yet: no dev API key is set (`context/changes/local-dev/local-dev-plan.md:71`, deferred to the user). There is also no recipe service, prompt, schema, UI, test or failure handling. The only existing code is the client registration (searched all `*.cs`, `*.razor`, `*.csproj`).

## Detailed Findings

### Why an LLM and not a recipe database (product fit)

- The decision to generate recipes with AI is recorded in the PRD (`prd.md:67`, FR-005) and the shape notes (`context/foundation/shape-notes.md:126`). Both record "Kontrargument rozważony: brak" (`prd.md:68`, `shape-notes.md:90`), so no alternative was weighed in either document. This research is the first written comparison.
- **Recipe database / recipe API (alternative).** It would give vetted recipes and predictable latency. It would also need (a) mapping arbitrary Polish product names to the database's ingredient IDs, (b) a "recipes with at most 2 missing ingredients" search over that database, and (c) a PRD change to FR-005. Mapping free-form names is exactly where an LLM helps. A database therefore swaps one LLM call for a matching problem plus a PRD revision. **Not recommended**, given a 2026-10-22 deadline and 3 after-hours weeks (`prd.md` frontmatter).
- **Hybrid (LLM picks, DB supplies).** Not considered further: it combines both integrations and is out of MVP scope.
- **LLM generation (current plan).** It handles any combination of products and works in Polish. It also gives a natural place to pass meal parameters such as meal type, time and servings (`prd.md:87`, slice S-05). Its weak points (made-up IDs, ignored limits, unpredictable output) are already covered by the app-side checks in `prd.md:81-95`.

### What the codebase already gives the generator

- `Pantry/ProductService.cs:40-57` `GetProductsAsync(userId)` returns `ProductList(UseFirst, Stored)`. Each item is a `ProductListItem(Id, Name, Category, Quantity, ExpiresOn, StorageLocation, IsExpiryDue)`, filtered by `p.UserId == userId` (`:46`). This is the prompt input. It already carries the `int Id` that the AI must echo back (`prd.md:89`), and `Id` was kept a compact `int` for this purpose (`context/archive/2026-10-03-pantry-add-products/plan.md:39`).
- `Program.cs:58-64` registers `AnthropicClient` with `ApiKey = builder.Configuration["ANTHROPIC_API_KEY"]` and `Timeout = 60 s`. `Program.cs:68-71` logs a warning and still starts when the key is missing.
- `Pantry/ProductLabels.cs` holds Polish labels for the enums, which can be reused in the prompt.
- `KitchenAssistant.Tests/ProductServiceTests.cs` uses SQLite in-memory and a fake `TimeProvider`. The test project has no mocking or AI package (inspected `KitchenAssistant.Tests.csproj`).

### Claude API facts that shape the design (source: bundled `claude-api` skill, model table cached 2026-09-25)

- **Structured output** is `output_config.format` with a JSON schema. In C# it is `OutputConfig = new OutputConfig { Format = new JsonOutputFormat { Schema = … } }`. It is supported on Claude Opus 5.5, Sonnet 5.5, Haiku 4.5 and other current models.
- **Schema limits that matter here.** These schema features are not supported: numeric `minimum`/`maximum`, string length limits and "complex array constraints". All objects need `additionalProperties: false`. The skill documents automatic client-side stripping of unsupported constraints **for the Python and TypeScript SDKs only**. In C#, keep "≤ 5 proposals", "≤ 2 missing" and similar limits out of the schema and enforce them in code. The PRD already asks for that (`prd.md:93`).
- **The first request with a new schema is slower** (one-time schema compilation, then cached for 24 h). Expect the first call after a schema change to be slower than later ones.
- **Forced tool use (`tool_choice` `any`/`tool`) returns 400** on Claude Opus 5.5 and Sonnet 5.5. Use structured output for the JSON response, not a forced tool call.
- **Refusals and truncation.** If `stop_reason` is `"refusal"` or `"max_tokens"`, the output may not match the schema. Check `StopReason` before parsing.
- **Retries vs. the 1-minute rule.** The skill's client-config notes say the default `max_retries` is 2 and that timeouts are retried, so wall-clock time can reach `timeout × (max_retries + 1)`. With `Timeout = 60 s` (`Program.cs:62`) and default retries, one user request could wait up to about 3 minutes. That breaks the 1-minute guardrail (`prd.md` NFR). Verify the C# retry default against the SDK, and set the retry count or a per-request deadline explicitly in the plan.
- **Models and per-token prices (USD per 1M tokens, input/output).** Claude Opus 5.5 $4/$20 (thinking always on, effort default `medium`). Claude Sonnet 5.5 $2/$10. Claude Haiku 4.5 $1/$5. Choosing the model is your decision. Measure it in the spike rather than guessing.

### Cost: rough estimate (inference, not measured)

Assumptions: about 1,500 input tokens per request (instructions plus about 30 products plus schema), about 3,000 visible output tokens (5 recipes with ingredients and steps), and thinking tokens ignored. Thinking is billed as output, so real cost is higher at higher effort. Under those assumptions, one request costs about $0.07 on Opus 5.5, about $0.03 on Sonnet 5.5 and about $0.02 on Haiku 4.5. At 15 users × 8 requests a month (120 requests), that is roughly $8, $4 and $2 a month. The Opus figure is close to the example $10 monthly spend limit in `context/changes/deployment/deployment-plan.md:56`. Replace these numbers with `response.Usage` from the spike.

### Latency: unknown until measured

No throughput figure was available in the inspected sources, so this research does not estimate seconds. Output length is the main cost driver, followed by effort/thinking and model size. The levers, in order of how little they change the product:
1. Lower effort, or a smaller or faster model.
2. Shorter recipes in the list (fewer steps, terse wording).
3. Stream the response so the user sees progress.
4. The split the roadmap already anticipates: one call for 5 short proposals, then one call for the full recipe on open. That second call lands in S-04 (`roadmap.md:139`).

## Code References

- `Program.cs:58-64` registers `AnthropicClient` (key from configuration, 60 s timeout)
- `Program.cs:68-71` logs a warning when the key is missing
- `KitchenAssistant.csproj:16` references the `Anthropic` 12.50.0 package
- `Pantry/ProductService.cs:40-57` `GetProductsAsync` is the per-user product list with IDs
- `Pantry/ProductService.cs:13-22` defines the `ProductListItem` record
- `Data/Product.cs:4-22` defines the `Product` entity (`int Id`, `UserId`, `Name`, `Category`, …)
- `Components/Pages/Products.razor:142` shows how a page reads `userId` from the auth state
- `.env.example:6-7` and `compose.yaml:34` pass `ANTHROPIC_API_KEY` through to the prod-like stack

## Architecture Insights

- The safest shape for S-02 is a `Recipes/` feature folder (by the `CLAUDE.md` convention; avoid clashing with a `Recipes.razor` page class). It would hold a generator service that (1) loads products through `ProductService`, (2) makes one structured-output call, (3) deserializes into C# records, and (4) checks the result in code: drop unknown product IDs, enforce the ≤ 5 / ≤ 2 limits, and apply the "always at home" list. Steps 3–4 are pure functions you can test against hand-written JSON, with no API call.
- Put the LLM call behind a small interface so tests and the UI can use a fake implementation. Only one manual/smoke test needs to hit the real API.
- Failure paths to plan for: missing key, timeout, 429/5xx, refusal, `max_tokens`, and invalid IDs. The deployment plan's App Insights alert catches AI failures in production (`deployment-plan.md:252`), but the page still needs a user-facing error state.

## Historical Context (from prior changes)

- `context/changes/local-dev/local-dev-plan.md:33,71` L2 is done, except the Anthropic dev key, which is deferred to you. It is a prerequisite for S-02 (`roadmap.md:109`).
- `context/changes/local-dev/local-dev-plan.md:99` corporate TLS inspection can break Anthropic calls (`UntrustedRoot`). The fix is to run off VPN or trust the corporate root CA, never to disable validation.
- `context/changes/deployment/deployment-plan.md:55-57` plans separate dev and prod keys and a prod workspace with a monthly spend limit (example $10).
- `context/changes/deployment/deployment-plan.md:115-116` the 60 s timeout was chosen because the SDK default of 10 min breaks the 1-minute guardrail. The retry multiplier above was not considered there.
- `context/archive/2026-10-03-pantry-add-products/plan.md:39` `Product.Id` was kept a stable `int` because S-02 sends products to the AI by ID.

## Related Research

Not applicable. No other `research.md` exists under `context/changes/**` or `context/archive/**`.

## Open Questions

1. **Latency and quality spike (blocks the shape of S-02).** With a dev key, send about 15–30 realistic Polish products in one structured-output request for 5 recipes. Record wall-clock time, `Usage` tokens and whether the returned IDs are valid. Try at least two model/effort settings. The result decides "one call" vs. "list then details" (`roadmap.md:112-114`).
2. **Model and effort choice.** This is your decision. Make it from the spike numbers and the spend limit.
3. **Retry/timeout budget.** Confirm the C# SDK's default `MaxRetries`, then choose a combination of timeout and retries that keeps a request under 1 minute.
4. **Prompt language.** Product names are Polish, so the recipes should come back in Polish. Decide whether to write the instructions in Polish or English. The spike can compare both.

## Follow-up 2026-10-04: Can the Claude Max subscription pay for the app's API calls?

**Short answer: no. Treat API usage as a separate, prepaid cost.** Source: general knowledge of Anthropic's product terms. The bundled `claude-api` skill does not cover subscription billing (searched `shared/**` for subscription/billing terms), so confirm on Anthropic's help center and terms pages.

- A Claude Max subscription covers your own use of the Claude apps and Claude Code. API calls with an API key (`ANTHROPIC_API_KEY`, `Program.cs:58`) are billed separately through the Claude Console from prepaid credits. The subscription does not include API credits.
- Using subscription login tokens to power your own app is not a permitted workaround, especially for an app that other people use (`prd.md` persona: up to a dozen users). The app needs a Console API key, as both plans already assume (`local-dev-plan.md:71`, `deployment-plan.md:55-57`).

**Where Max does save money (no API spend):**
- **Writing the code.** The whole S-02 implementation can be built with Claude Code under the subscription.
- **Quality half of the spike (Open Question 1).** Paste a realistic product list and the draft prompt into Claude (claude.ai or Claude Code) and judge the recipe quality, the Polish and how well it follows the limits. Iterate on the prompt wording there. This does **not** measure API latency, token usage or structured-output behaviour, so a few real API calls are still needed.
- **Developing without a key.** With the generator behind an interface (see Architecture Insights), the UI, the scoring and the tests can run against a fake implementation that returns hand-written JSON. Only the final smoke test and the latency measurement need the real API.

**Expected API spend (inference, using the cost estimate above):** a spike of about 20–30 calls at up to about $0.07 each is roughly $2. Set a low spend limit on the dev key's workspace in the Console.

## Follow-up 2026-10-04: Is there a free (limited) LLM API to use instead?

**Short answer:** free tiers exist, and they are fine for your own development and prompt experiments. None is a good basis for the deployed app. Recommendation: **build against a fake generator, keep Anthropic for the real calls**, and spend about $2 on the latency spike. Checked 2026-10-04; free tiers change often.

| Option | What's free | Structured output | Catch for this project |
|---|---|---|---|
| **Google Gemini API** (AI Studio key) | Free tier on most text models, rate-limited per minute and per day ([pricing](https://ai.google.dev/gemini-api/docs/pricing)). | Yes (JSON schema) | Free-tier content is "used to improve our products", and human reviewers may read it. **The Gemini API terms say: "You may use only Paid Services when making API Clients available to users in the European Economic Area, Switzerland, or the United Kingdom"** ([terms](https://ai.google.dev/gemini-api/terms)). Your users are in Poland, so the deployed app cannot use the free tier. Testing it yourself during development is allowed. |
| **Groq** (open-weight models: gpt-oss, Qwen, Llama) | Free plan; e.g. `openai/gpt-oss-120b`: 30 RPM, 1K RPD, **8K TPM**, 200K TPD ([rate limits](https://console.groq.com/docs/rate-limits)). | `json_schema` on supported models | At about 4.5K tokens per request (estimate above), 8K TPM allows about 1 request per minute. That is enough for experiments, not for users. Open models' Polish recipe quality is unmeasured. |
| **Local model via Ollama** | Fully free; runs on your PC. | Yes (JSON schema `format`) | It can't run on the planned Azure App Service B1. Small local models are weaker at Polish and at following the "use these IDs" rule. Latency depends on your hardware. Good for offline development only. |

**Why switching doesn't save the S-02 spike:** the open question is whether *the production model* returns 5 good recipes with valid IDs within 1 minute (`roadmap.md:112-114`). Measurements on a different model or provider don't carry over to Claude. Prompts also behave differently across models.

**Switching providers costs work.** The project uses the `Anthropic` C# SDK (`KitchenAssistant.csproj:16`) and Anthropic keys in both plans. Supporting several providers would mean coding against the `Microsoft.Extensions.AI` `IChatClient` abstraction instead. The Anthropic C# SDK supports `IChatClient` per the bundled `claude-api` skill. Whether its structured-output mapping is equivalent to `OutputConfig.Format` is **unverified**. That is extra scope against the 2026-10-22 deadline.

**Zero-cost development path (no provider switch):** put the generator behind an interface (Architecture Insights). Use a fake implementation that returns hand-written JSON for the UI, scoring and tests. Use the Claude Max subscription to iterate on prompt wording by hand (previous follow-up). Make real Anthropic calls only for the spike and the smoke test.

## Decision 2026-10-04: zero-cost development path (user choice)

The user chose the "zero-cost until the end" path. Settled for `/10x-plan`:

1. **Provider stays Anthropic.** No provider switch and no `IChatClient` multi-provider layer in S-02.
2. **The generator sits behind an interface.** Its real implementation calls Anthropic with structured output (`OutputConfig.Format`). A fake implementation returns hand-written JSON. The UI, the result checks (unknown IDs, ≤ 5 proposals, ≤ 2 missing, the "always at home" list) and the tests are all built against the fake, with no API key needed.
3. **Prompt wording is iterated by hand** in Claude under the user's Max subscription (no API spend).
4. **Real API calls come last.** Once a dev key is set (`local-dev-plan.md:71`) with a low Console spend limit: the latency/quality spike (Open Question 1, about $2) and one smoke test. The spike result still decides one call vs. "list then details" (`roadmap.md:112-114`). Plan for that as a gate late in the change, not as a prerequisite to start coding.

Still open for the plan: Open Questions 2–4 (model/effort, the retry/timeout budget, prompt language).
