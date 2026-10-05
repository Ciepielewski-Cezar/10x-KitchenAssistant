# Spike: live latency and quality of recipe generation (S-02, Phase 3)

## Purpose

Phase 3 of `plan.md` is the only step that calls the real Anthropic API. It answers the open question from the roadmap's S-02 Unknowns:

> Can one call generate 5 full recipes within a minute, or does generation have to be split (a list of proposals first, then the full recipe)?

The measurements below decide the committed `Recipes:Model` / `Recipes:Effort` defaults in `appsettings.json`. They also decide whether S-04 needs a second AI call. Budget: about 10–20 calls, well under $2.

Status: **not run yet** (template prepared 2026-10-05).

## Runbook (you run it; the agent fills the table from your log lines)

### 1. Key and spend limit

1. In the Anthropic Console, use a **dev** workspace (not the future prod one) and set a low monthly spend limit on it, e.g. $5.
2. Create a dev API key in that workspace. **Never paste the key into chat**, and never paste the output of `dotnet user-secrets list`, because it prints values.
3. In a terminal in the repo root (next to `KitchenAssistant.csproj`), run:
   ```powershell
   dotnet user-secrets set ANTHROPIC_API_KEY <dev-key>
   ```
   Optional: to keep the key out of PowerShell history (PowerShell 7.1+), run this instead:
   ```powershell
   $k = Read-Host 'Dev key' -MaskInput; dotnet user-secrets set ANTHROPIC_API_KEY $k; Remove-Variable k
   ```

### 2. Switch Development to the real generator (not committed)

`appsettings.Development.json` sets `Recipes:Generator = Fake`, and that file stays committed as is. User-secrets override it locally:

```powershell
dotnet user-secrets set Recipes:Generator Anthropic
```

When the spike is done, go back to the fake generator:

```powershell
dotnet user-secrets remove Recipes:Generator
```

You can keep the key. With the fake generator active, it is not used.

### 3. Network

If you are on a work laptop or VPN with TLS inspection, disconnect from the VPN first. Calls can otherwise fail with `The remote certificate is invalid` / `UntrustedRoot` (`context/changes/local-dev/local-dev-plan.md:99`). Never disable certificate validation.

### 4. Start the app

```powershell
dotnet run --launch-profile https
```

Open https://localhost:7020. Check the startup log: the warning `ANTHROPIC_API_KEY is not configured; AI recipe generation will fail until it is set.` must **not** appear. That warning's absence is the 3.2 check.

### 5. Account and pantry

1. Register a new throwaway account, e.g. `spike@kitchen.test`. It exists only in your LocalDB. Confirm it with the on-screen link, then log in. A fresh account means the pantry is exactly the list below.
2. At `/products`, add the 25 products from [Pantry](#pantry-25-products). Leave the expiry date and storage location empty. The form keeps the last category after each add, so enter one category's products in a row.

### 6. Calls

1. Open `/recipes` and click „Zaproponuj przepisy”. **Call 0** compiles the schema (a one-time cost, cached about 24 h), so it is slower. Record it in row 0, but leave it out of the statistics.
2. Then click „Zaproponuj przepisy” **10 more times** (rows 1–10). Wait for each result before the next click. The button stays available after a result.
3. For each call, write a short quality note: are titles, ingredients and steps in Polish? Are the steps logical and in a sensible order? Does the recipe fit „obiad, do 30 minut, 1 porcja”? Is anything odd, such as made-up products or a duplicated recipe?

### 7. Which log lines to copy

Each successful click writes two Information lines to the console (each log entry spans two lines in the default console format: the category line and the message). Copy both entries, for every call:

```
info: KitchenAssistant.Recipes.AnthropicRecipeGenerator[0]
      Recipe generation call: model claude-sonnet-5-5, effort low, 23456 ms, stop reason end_turn, input tokens 1234, output tokens 3456, thinking tokens 789.
info: KitchenAssistant.Recipes.RecipeService[0]
      Recipe classification: 5 recipes returned, 5 proposals kept; ingredients owned by ID 18, owned by name 1, always at home 7, missing 3, unknown IDs 0.
```

(The numbers above are made-up examples.)

Also copy any of these if they appear:

- `warn: … RecipeService` — `The recipe generator returned N product IDs not on the user's list …`
- `fail: … RecipeService` — timeout after the 60 s deadline, a per-attempt timeout, `Recipe generation failed: …` (for example `The model stopped with 'max_tokens' …`), or `The Anthropic API call for recipe generation failed.` together with the exception message below it. A 400 on the schema (for example about the nullable `productId`) shows up here.

These lines contain no secrets: no key, no product list, no recipe text. **You can paste them into chat as they are**, together with your quality notes, and the agent fills in the table and the summary.

If a call hits the 60 s deadline, the `Recipe generation call:` line is not written. The row then gets `timeout (> 60 s)` with no token numbers.

## Pantry (25 products)

Categories as the form labels them (`Pantry/ProductLabels.cs`): **Zużyj w pierwszej kolejności** and **W szafkach i zamrażalniku**. Quantity is the optional „Ilość” field. None of the names matches an entry on the „zawsze w domu” list (`RecipeClassifier.AlwaysAtHomeItems`).

| # | Nazwa | Kategoria | Ilość |
|---|---|---|---|
| 1 | pierś z kurczaka | Zużyj w pierwszej kolejności | 500 g |
| 2 | pieczarki | Zużyj w pierwszej kolejności | 250 g |
| 3 | cukinia | Zużyj w pierwszej kolejności | 1 szt. |
| 4 | pomidory | Zużyj w pierwszej kolejności | 4 szt. |
| 5 | śmietana 18% | Zużyj w pierwszej kolejności | 200 ml |
| 6 | jogurt naturalny | Zużyj w pierwszej kolejności | 400 g |
| 7 | szpinak | Zużyj w pierwszej kolejności | 150 g |
| 8 | mleko | Zużyj w pierwszej kolejności | 1 l |
| 9 | papryka czerwona | Zużyj w pierwszej kolejności | 2 szt. |
| 10 | ser feta | Zużyj w pierwszej kolejności | 200 g |
| 11 | jajka | W szafkach i zamrażalniku | 10 szt. |
| 12 | makaron penne | W szafkach i zamrażalniku | 500 g |
| 13 | ryż | W szafkach i zamrażalniku | 1 kg |
| 14 | kasza gryczana | W szafkach i zamrażalniku | 400 g |
| 15 | mąka pszenna | W szafkach i zamrażalniku | 1 kg |
| 16 | ziemniaki | W szafkach i zamrażalniku | 2 kg |
| 17 | cebula | W szafkach i zamrażalniku | 5 szt. |
| 18 | marchew | W szafkach i zamrażalniku | 1 kg |
| 19 | ser żółty | W szafkach i zamrażalniku | 300 g |
| 20 | masło | W szafkach i zamrażalniku | 200 g |
| 21 | passata pomidorowa | W szafkach i zamrażalniku | 500 g |
| 22 | ciecierzyca z puszki | W szafkach i zamrażalniku | 400 g |
| 23 | groszek mrożony | W szafkach i zamrażalniku | 450 g |
| 24 | tuńczyk w puszce | W szafkach i zamrażalniku | 2 puszki |
| 25 | płatki owsiane | W szafkach i zamrażalniku | 500 g |

## Measurements: Sonnet 5.5 / effort low

Model `claude-sonnet-5-5`, effort `low`, `MaxTokens` 16000, deadline 60 s, meal parameters: obiad, do 30 minut, 1 porcja.

Column sources: elapsed s = `ElapsedMs` / 1000 from the generator line (it covers the SDK call, including any retry); tokens and stop reason come from the same line; recipes = `proposals kept` (with `recipes returned` in brackets when different); owned-by-ID, owned-by-name and unknown-ID come from the classification line. Counts cover the ingredients of the kept proposals.

| # | elapsed s | stop reason | input tok | output tok | thinking tok | recipes | owned-by-ID | owned-by-name | unknown-ID | quality note |
|---|---|---|---|---|---|---|---|---|---|---|
| 0 (schema compile, excluded) | | | | | | | | | | |
| 1 | | | | | | | | | | |
| 2 | | | | | | | | | | |
| 3 | | | | | | | | | | |
| 4 | | | | | | | | | | |
| 5 | | | | | | | | | | |
| 6 | | | | | | | | | | |
| 7 | | | | | | | | | | |
| 8 | | | | | | | | | | |
| 9 | | | | | | | | | | |
| 10 | | | | | | | | | | |

## Summary

Rows 1–10 only (row 0 excluded).

- **Median latency:** _TBD_ s
- **Max latency:** _TBD_ s (guardrail: every call ≤ 60 s)
- **Calls within 60 s:** _TBD_ / 10
- **Stop reasons:** _TBD_ (expected: all `end_turn`)
- **Average tokens per call:** input _TBD_, output _TBD_, thinking _TBD_
- **Estimated cost per request:** _TBD_ USD
  - Pricing assumption: Sonnet 5.5 at $2 per 1M input tokens and $10 per 1M output tokens. Source: `research.md` § "Claude API facts", from the bundled `claude-api` skill's model table, cached 2026-09-25. Thinking is billed as output.
  - Formula: `(avg input × 2 + avg output × 10) / 1,000,000`.
  - TODO: confirm in the Console usage view that `output tokens` already includes `thinking tokens`. If it does not, add thinking to output in the formula. Also check the prices against the current Anthropic pricing page before writing down the figure.
- **ID validity rate:** _TBD_ % = Σ owned-by-ID / (Σ owned-by-ID + Σ unknown-ID). This is the share of non-null product IDs from the AI that were on the user's list.
- **Name-fallback rate:** _TBD_ % = Σ owned-by-name / (Σ owned-by-ID + Σ owned-by-name). This is the share of owned ingredients for which the AI gave no ID and the exact-name match decided ownership.
- **Quality:** _TBD_ (Polish throughout? steps logical? fits obiad / ≤ 30 min / 1 porcja?)
- **Verdict:** _TBD_ — one of:
  - **Pass**: every measured call ≤ 60 s → keep or adjust the defaults, then answer the roadmap S-02 unknown („jedno wywołanie wystarcza”).
  - **Fail**: apply the levers below.
- **Chosen defaults** (to commit in `appsettings.json` → `Recipes`): `Model` = _TBD_, `Effort` = _TBD_, `MaxTokens` = _TBD_.

## Levers (only if a measured call exceeds 60 s)

Apply them in order. For each lever, measure **≥ 5 calls** on the same pantry. Try each setting through user-secrets first, so nothing is committed before the decision. Remove the override afterwards with `dotnet user-secrets remove <key>`.

### Lever 1: Haiku 4.5

- Setting: `dotnet user-secrets set Recipes:Model claude-haiku-4-5` (effort unchanged).
- Caveat: if the API rejects the `effort` parameter for this model, the change needs code, not just config. Paste the error line, and the agent adjusts it.

| # | elapsed s | stop reason | input tok | output tok | thinking tok | recipes | owned-by-ID | owned-by-name | unknown-ID | quality note |
|---|---|---|---|---|---|---|---|---|---|---|
| 1 | | | | | | | | | | |
| 2 | | | | | | | | | | |
| 3 | | | | | | | | | | |
| 4 | | | | | | | | | | |
| 5 | | | | | | | | | | |

Result: _TBD_

### Lever 2: terser recipes (prompt change)

- Change: the agent shortens the requested output in `Recipes/RecipePrompt.cs`, e.g. fewer and shorter steps and a one-sentence summary. This is a code change, so it is reviewed and committed.

| # | elapsed s | stop reason | input tok | output tok | thinking tok | recipes | owned-by-ID | owned-by-name | unknown-ID | quality note |
|---|---|---|---|---|---|---|---|---|---|---|
| 1 | | | | | | | | | | |
| 2 | | | | | | | | | | |
| 3 | | | | | | | | | | |
| 4 | | | | | | | | | | |
| 5 | | | | | | | | | | |

Result: _TBD_

### Lever 3: stop and re-plan

If neither lever fits within 60 s, stop. Re-plan the split (a list of proposals first, then the full recipe on open) together with S-04, and record that escalation in the roadmap's S-02 Unknowns.

### Schema rejection

If the API rejects the nullable `productId` (`"type": ["integer", "null"]`), the agent switches it to `anyOf` in `Recipes/RecipeSchema.cs`. Re-run from call 0, because the schema changed.

## Smoke test: production-like stack (plan step 4)

1. Put the dev key in `.env` (git-ignored, next to `compose.yaml`) as `ANTHROPIC_API_KEY=<dev-key>`, with no quotes. Do not paste `.env` or the output of `docker compose config` into chat, because both contain secrets.
2. Run `./scripts/local-prod.ps1`. It rebuilds the image so the new code is included. Production uses the Anthropic generator from `appsettings.json` (no fake is allowed there).
3. Open http://localhost:8090. Register a new account (in Production, registration logs you in directly) and add a few products from the pantry above.
4. At http://localhost:8090/recipes, click „Zaproponuj przepisy” once.
5. Copy the two lines `Recipe generation call:` and `Recipe classification:` from `docker compose logs app`.

Result: _TBD_ (proposals shown yes/no, elapsed s, stop reason, any errors)

## Cleanup

- `dotnet user-secrets remove Recipes:Generator`, so Development uses the fake again.
- Also remove any lever overrides you set (`Recipes:Model`, `Recipes:Effort`).
- `./scripts/local-prod.ps1 -Down` stops the stack and keeps the data.
