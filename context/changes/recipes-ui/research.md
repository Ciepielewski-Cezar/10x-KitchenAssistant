---
date: 2026-10-06T20:01:10+02:00
researcher: Claude (Opus 5.5) for Cezar Ciepielewski
git_commit: 290338e
branch: main
repository: Ciepielewski-Cezar/10x-KitchenAssistant
topic: "Konfiguracja stylów i użycie klas (tokeny, komponenty UI, twarde kolory, brakujące prymitywy)"
tags: [research, ui, styles, bootstrap, design-tokens, recipes]
status: complete
last_updated: 2026-10-06
last_updated_by: Claude (Opus 5.5)
---

# Research: konfiguracja stylów i użycie klas

**Date**: 2026-10-06T20:01:10+02:00
**Researcher**: Claude (Opus 5.5) for Cezar Ciepielewski
**Git Commit**: 290338e (working tree with uncommitted changes only under `.claude/`; no inspected file has local edits)
**Branch**: main
**Repository**: Ciepielewski-Cezar/10x-KitchenAssistant

## Research Question

Przeanalizuj konfigurację stylów i użycie klas w tym projekcie. Wypisz:

1. Dokładną ścieżkę do głównego pliku stylów oraz listę zdefiniowanych zmiennych w `:root` i `.dark`.
2. Zmienne, które są poprawnie publikowane w bloku `@theme` lub `@theme inline`.
3. Listę komponentów obecnych fizycznie w katalogu `src/components/ui`.
4. Zestawienie plików w `src`, które używają twardo zakodowanych klas kolorów (np. `bg-purple-*`, `text-blue-*`) zamiast klas semantycznych (`bg-primary`, `text-muted-foreground`).
5. Brakujące prymitywy UI, które będą niezbędne do wyświetlenia danych w docelowym widoku.

## Summary

**Pytanie zakłada stack Tailwind v4 + shadcn/ui, którego w tym repo nie ma.** To jest Blazor (.NET 10) z **Bootstrap 5.3.3** w postaci skompilowanego CSS ([bootstrap.css:3](wwwroot/lib/bootstrap/dist/css/bootstrap.css:3)) i zwykłym CSS (globalny `app.css` + izolowane `*.razor.css`). W repo nie istnieje katalog `src/`, nie ma Tailwinda, `@theme`, klasy `.dark` ani `src/components/ui` (wyszukiwanie `data-bs-theme|prefers-color-scheme|\.dark\b|@theme|tailwind` w plikach śledzonych przez git poza `wwwroot/lib`, `.claude`, `context` — 0 trafień). Każde pytanie odpowiada więc na swój odpowiednik w Bootstrapie (wariant „Component library with a theme / CSS variables” z `/10x-ui`):

1. **Główny plik stylów aplikacji:** `wwwroot/app.css` — definiuje **0** zmiennych CSS. Wartości (tokeny) pochodzą z `wwwroot/lib/bootstrap/dist/css/bootstrap.css` (ładowany jako `.min.css`, [App.razor:9](Components/App.razor:9)): 117 zmiennych w `:root, [data-bs-theme=light]` (L7–L126) + 6 breakpointów w drugim `:root` (L778) oraz 52 zmienne w `[data-bs-theme=dark]` (L128–L182) — odpowiednik `.dark`.
2. **Odpowiednik `@theme`:** w Bootstrapie „publikacją” jest warstwa skompilowanych klas komponentów. Część klas używanych w widokach czyta tokeny (`text-muted`, `text-danger`, `text-bg-*`, `alert-danger`, `list-group-item-warning`, `card`), ale `.btn-primary`, `.btn-outline-danger` i `.form-control:focus` mają **wpisane literały hex** — zmiana `--bs-primary` ich nie przekolorowuje. Tryb ciemny jest martwy: blok `[data-bs-theme=dark]` istnieje, ale nic w aplikacji nie ustawia `data-bs-theme`.
3. **`src/components/ui` nie istnieje.** Nie ma żadnego katalogu z generycznymi prymitywami UI. Współdzielone komponenty to tylko layout (`Components/Layout/`: 3 komponenty) i komponenty Identity (`Components/Account/Shared/`: 7 komponentów).
4. **Twarde kolory:** w 55 inspekcjonowanych plikach (`Components/**/*.razor`, `Components/**/*.css`, `wwwroot/app.css`) literały kolorów są w 4 plikach CSS (`app.css`, `MainLayout.razor.css`, `NavMenu.razor.css`, `ReconnectModal.razor.css`) plus jedna klasa o stałym motywie (`navbar-dark`, [NavMenu.razor:5](Components/Layout/NavMenu.razor:5)). Strony (`Components/Pages/*`, `Components/Account/**`) używają wyłącznie klas rolowych Bootstrapa — 0 literałów kolorów.
5. **Brakujące prymitywy dla `/recipes`** (przyjęty widok docelowy, patrz *Open Questions*): komponent Badge statusu, RecipeCard, podsumowanie oceny „Masz X z Y” (meter), Alert z akcją, stany ładowania (spinner + skeleton), EmptyState, pole formularza i segmented control dla parametrów posiłku (S-05), disclosure dla szczegółów (S-04) oraz brakująca ikona w nawigacji.

## Detailed Findings

### 1. Pliki stylów i zmienne

**Kolejność ładowania** ([App.razor:9-11](Components/App.razor:9)):

1. `lib/bootstrap/dist/css/bootstrap.min.css` — tokeny + komponenty Bootstrapa 5.3.3.
2. `app.css` — globalne nadpisania aplikacji (**główny plik stylów aplikacji**: `wwwroot/app.css`, ścieżka bezwzględna `C:\Users\cciepielewski\source\repos\x10_devs\10x_poc\wwwroot\app.css`).
3. `KitchenAssistant.styles.css` — bundel CSS isolation generowany z `Components/Layout/MainLayout.razor.css`, `NavMenu.razor.css`, `ReconnectModal.razor.css` (to jedyne 3 pliki `*.razor.css` w repo).

**Zmienne zdefiniowane przez aplikację:** 0 — `app.css` i trzy `*.razor.css` nie zawierają żadnej deklaracji `--*:` (wyszukiwanie `^\s*--[\w-]+:` — 0 trafień). Jedyne miejsce, w którym kod aplikacji **czyta** token Bootstrapa, to [app.css:54](wwwroot/app.css:54) (`var(--bs-secondary-color)`; `git grep 'var\(--bs-'` poza `wwwroot/lib` — 1 trafienie).

**`:root, [data-bs-theme=light]`** — [bootstrap.css:7-126](wwwroot/lib/bootstrap/dist/css/bootstrap.css:7), 117 zmiennych:

- Paleta (14): `--bs-blue`, `--bs-indigo`, `--bs-purple`, `--bs-pink`, `--bs-red`, `--bs-orange`, `--bs-yellow`, `--bs-green`, `--bs-teal`, `--bs-cyan`, `--bs-black`, `--bs-white`, `--bs-gray`, `--bs-gray-dark`
- Skala szarości (9): `--bs-gray-100` … `--bs-gray-900`
- Kolory rolowe (8): `--bs-primary`, `--bs-secondary`, `--bs-success`, `--bs-info`, `--bs-warning`, `--bs-danger`, `--bs-light`, `--bs-dark`
- Kanały RGB ról (8): `--bs-{primary,secondary,success,info,warning,danger,light,dark}-rgb`
- Warianty ról (3 × 8 = 24): `--bs-{rola}-text-emphasis`, `--bs-{rola}-bg-subtle`, `--bs-{rola}-border-subtle`
- `--bs-white-rgb`, `--bs-black-rgb`
- Typografia i tło (14): `--bs-font-sans-serif`, `--bs-font-monospace`, `--bs-gradient`, `--bs-body-font-family`, `--bs-body-font-size`, `--bs-body-font-weight`, `--bs-body-line-height`, `--bs-body-color`, `--bs-body-color-rgb`, `--bs-body-bg`, `--bs-body-bg-rgb`, `--bs-emphasis-color`, `--bs-emphasis-color-rgb`, `--bs-heading-color`
- Powierzchnie drugiego/trzeciego planu (8): `--bs-secondary-color`, `--bs-secondary-color-rgb`, `--bs-secondary-bg`, `--bs-secondary-bg-rgb`, `--bs-tertiary-color`, `--bs-tertiary-color-rgb`, `--bs-tertiary-bg`, `--bs-tertiary-bg-rgb`
- Linki, kod, zaznaczenie (9): `--bs-link-color`, `--bs-link-color-rgb`, `--bs-link-decoration`, `--bs-link-hover-color`, `--bs-link-hover-color-rgb`, `--bs-code-color`, `--bs-highlight-color`, `--bs-highlight-bg`
- Obramowania i promienie (11): `--bs-border-width`, `--bs-border-style`, `--bs-border-color`, `--bs-border-color-translucent`, `--bs-border-radius`, `--bs-border-radius-sm`, `--bs-border-radius-lg`, `--bs-border-radius-xl`, `--bs-border-radius-xxl`, `--bs-border-radius-2xl`, `--bs-border-radius-pill`
- Cienie (4): `--bs-box-shadow`, `--bs-box-shadow-sm`, `--bs-box-shadow-lg`, `--bs-box-shadow-inset`
- Focus (3): `--bs-focus-ring-width`, `--bs-focus-ring-opacity`, `--bs-focus-ring-color`
- Walidacja (4): `--bs-form-valid-color`, `--bs-form-valid-border-color`, `--bs-form-invalid-color`, `--bs-form-invalid-border-color`

**Drugi `:root`** — [bootstrap.css:778-785](wwwroot/lib/bootstrap/dist/css/bootstrap.css:778), 6 zmiennych: `--bs-breakpoint-xs|sm|md|lg|xl|xxl`.

**`[data-bs-theme=dark]` (odpowiednik `.dark`)** — [bootstrap.css:128-182](wwwroot/lib/bootstrap/dist/css/bootstrap.css:128), `color-scheme: dark` + 52 zmienne, nadpisujące podzbiór z `:root`:

- `--bs-body-color(-rgb)`, `--bs-body-bg(-rgb)`, `--bs-emphasis-color(-rgb)`, `--bs-secondary-color(-rgb)`, `--bs-secondary-bg(-rgb)`, `--bs-tertiary-color(-rgb)`, `--bs-tertiary-bg(-rgb)` (14)
- `--bs-{rola}-text-emphasis`, `--bs-{rola}-bg-subtle`, `--bs-{rola}-border-subtle` dla 8 ról (24)
- `--bs-heading-color`, `--bs-link-color`, `--bs-link-hover-color`, `--bs-link-color-rgb`, `--bs-link-hover-color-rgb`, `--bs-code-color`, `--bs-highlight-color`, `--bs-highlight-bg` (8)
- `--bs-border-color`, `--bs-border-color-translucent` (2)
- `--bs-form-valid-color`, `--bs-form-valid-border-color`, `--bs-form-invalid-color`, `--bs-form-invalid-border-color` (4)

Blok ciemny **nie** redefiniuje kolorów rolowych (`--bs-primary` itd.) ani ich `-rgb`.

### 2. Odpowiednik `@theme` / `@theme inline`: które tokeny faktycznie docierają do widoków

W Tailwind v4 `@theme inline` publikuje zmienne jako klasy. W Bootstrapie nie ma takiego kroku w repo — „publikacją” są klasy komponentów w skompilowanym `bootstrap.css`, który jest vendorowany i nie jest budowany z Sass w tym projekcie (brak `package.json`, `*.scss`; `git ls-files` — 0 trafień). Poniżej klasy rzeczywiście użyte w `Products.razor` i `RecipeSuggestions.razor` i to, czy czytają token.

**Czytają tokeny (reagują na zmianę `:root` i na `[data-bs-theme=dark]`):**

| Klasa | Tokeny | Źródło | Użycie w widoku |
| --- | --- | --- | --- |
| `.text-muted` | `--bs-secondary-color` | [bootstrap.css:8557](wwwroot/lib/bootstrap/dist/css/bootstrap.css:8557) | [Products.razor:108](Components/Pages/Products.razor:108), [:123](Components/Pages/Products.razor:123), [:127](Components/Pages/Products.razor:127); [RecipeSuggestions.razor:66](Components/Pages/RecipeSuggestions.razor:66), [:77](Components/Pages/RecipeSuggestions.razor:77) |
| `.text-danger` | `--bs-danger-rgb` | [bootstrap.css:8527](wwwroot/lib/bootstrap/dist/css/bootstrap.css:8527) | [Products.razor:70](Components/Pages/Products.razor:70) |
| `.text-bg-success`, `.text-bg-warning` (i analogicznie `-secondary`) | `--bs-{rola}-rgb`; kolor tekstu literalny `#fff` / `#000` | [bootstrap.css:6854](wwwroot/lib/bootstrap/dist/css/bootstrap.css:6854), [:6864](wwwroot/lib/bootstrap/dist/css/bootstrap.css:6864) | [RecipeSuggestions.razor:112-114](Components/Pages/RecipeSuggestions.razor:112), [Products.razor:131](Components/Pages/Products.razor:131) |
| `.alert-danger` | `--bs-danger-text-emphasis`, `-bg-subtle`, `-border-subtle` | [bootstrap.css:4932](wwwroot/lib/bootstrap/dist/css/bootstrap.css:4932) | [Products.razor:76](Components/Pages/Products.razor:76); [RecipeSuggestions.razor:22](Components/Pages/RecipeSuggestions.razor:22), [:47](Components/Pages/RecipeSuggestions.razor:47) |
| `.list-group-item-warning` | `--bs-warning-text-emphasis`, `-bg-subtle`, `-border-subtle` | [bootstrap.css:5306](wwwroot/lib/bootstrap/dist/css/bootstrap.css:5306) | [Products.razor:115](Components/Pages/Products.razor:115) |
| `.card` | `--bs-border-width`, `--bs-border-color-translucent`, `--bs-border-radius` | [bootstrap.css:4380](wwwroot/lib/bootstrap/dist/css/bootstrap.css:4380) | [RecipeSuggestions.razor:57](Components/Pages/RecipeSuggestions.razor:57) |
| `.badge` | `--bs-border-radius`; `--bs-badge-color: #fff` literalnie | [bootstrap.css:4831](wwwroot/lib/bootstrap/dist/css/bootstrap.css:4831) | jw. jak `text-bg-*` |

**Nie czytają tokenów (literały w skompilowanym CSS — odpowiednik „surowych kolorów wpisanych w `@theme inline`”):**

- `.btn-primary` — `--bs-btn-bg: #0d6efd`, `--bs-btn-hover-bg: #0b5ed7` itd. ([bootstrap.css:3056-3069](wwwroot/lib/bootstrap/dist/css/bootstrap.css:3056)); użyte w [Products.razor:66](Components/Pages/Products.razor:66), [RecipeSuggestions.razor:32](Components/Pages/RecipeSuggestions.razor:32). Zmiana `--bs-primary` w `:root` nie zmieni koloru przycisku.
- `.btn-outline-danger` — `--bs-btn-color: #dc3545` ([bootstrap.css:3277](wwwroot/lib/bootstrap/dist/css/bootstrap.css:3277)); użyte w [RecipeSuggestions.razor:49](Components/Pages/RecipeSuggestions.razor:49).
- `.form-control:focus` — `border-color: #86b7fe`, `box-shadow: … rgba(13, 110, 253, 0.25)` ([bootstrap.css:2143-2148](wwwroot/lib/bootstrap/dist/css/bootstrap.css:2143)), a nie `--bs-focus-ring-color`.

**Nadpisania w `app.css`, które omijają warstwę tokenów** (aplikacja ustawia literały zamiast przedefiniować zmienną):

| Reguła | Literał | Token, który powinien to nieść |
| --- | --- | --- |
| `a, .btn-link` — [app.css:5-7](wwwroot/app.css:5) | `#006bb7` | `--bs-link-color` (`#0d6efd`, [bootstrap.css:96](wwwroot/lib/bootstrap/dist/css/bootstrap.css:96)); literał nie przełączy się w trybie ciemnym, który redefiniuje `--bs-link-color` |
| `.btn-primary` — [app.css:9-13](wwwroot/app.css:9) | `#fff`, `#1b6ec2`, `#1861ac` | `--bs-primary` (`#0d6efd`, [bootstrap.css:32](wwwroot/lib/bootstrap/dist/css/bootstrap.css:32)) / `--bs-btn-*` |
| focus `.btn`, `.form-control`, `.form-check-input` — [app.css:15-17](wwwroot/app.css:15) | `white`, `#258cfb` | `--bs-focus-ring-color` ([bootstrap.css:121](wwwroot/lib/bootstrap/dist/css/bootstrap.css:121)) |
| `.valid.modified` — [app.css:27-29](wwwroot/app.css:27) | `#26b050` | `--bs-form-valid-color` |
| `.invalid`, `.validation-message` — [app.css:31-37](wwwroot/app.css:31) | `#e50000` | `--bs-form-invalid-color` (`#dc3545`, [bootstrap.css:124](wwwroot/lib/bootstrap/dist/css/bootstrap.css:124)) |
| `.darker-border-checkbox` — [app.css:49-51](wwwroot/app.css:49) | `#929292` | `--bs-border-color` |

Wnioskowanie z kaskady (nie zweryfikowane w przeglądarce): `app.css:9-13` ustawia `background-color` bezpośrednio, a nie `--bs-btn-bg`. Reguły Bootstrapa `.btn:hover` / `.btn:disabled` (wyższa specyficzność niż `.btn-primary`) czytają `--bs-btn-hover-bg` / `--bs-btn-disabled-bg` z [bootstrap.css:3061](wwwroot/lib/bootstrap/dist/css/bootstrap.css:3061) i dalej, więc stan default ma odcień `#1b6ec2`, a hover/disabled — odcienie Bootstrapa (`#0b5ed7` / `#0d6efd`). To dwa „primary” na jednym przycisku. Przycisk „Zaproponuj przepisy” jest disabled podczas generowania ([RecipeSuggestions.razor:32](Components/Pages/RecipeSuggestions.razor:32)), więc ta zmiana odcienia jest widoczna w głównym flow.

**Tryb ciemny:** `[data-bs-theme=dark]` jest zdefiniowany (L128–L182), ale w Bootstrap 5.3 aktywuje go wyłącznie atrybut `data-bs-theme`, a w skompilowanym CSS nie ma `@media (prefers-color-scheme …)` (wyszukiwanie w `bootstrap.css` — 0 trafień). Aplikacja nigdzie nie ustawia `data-bs-theme` (0 trafień poza `wwwroot/lib`). Wszystkie 52 zmienne ciemne są więc w obecnym stanie nieosiągalne. Dodatkowo [MainLayout.razor.css:80](Components/Layout/MainLayout.razor.css:80) wymusza `color-scheme: light only` dla `#blazor-error-ui`.

### 3. Komponenty UI fizycznie w repo

- `src/components/ui` — **nie istnieje**; katalog `src/` też nie istnieje (sprawdzone `Test-Path src` → False).
- Wszystkie komponenty Razor w `Components/` (pełna lista z `git ls-files`):
  - `Components/Layout/`: `MainLayout.razor`, `NavMenu.razor`, `ReconnectModal.razor` (+ `.razor.css`, `ReconnectModal.razor.js`).
  - `Components/Account/Shared/` (specyficzne dla Identity): `ExternalLoginPicker.razor`, `ManageLayout.razor`, `ManageNavMenu.razor`, `PasskeySubmit.razor` (+ `.razor.js`), `RedirectToLogin.razor`, `ShowRecoveryCodes.razor`, `StatusMessage.razor`.
  - `Components/Pages/`: `Error.razor`, `Home.razor`, `NotFound.razor`, `Products.razor`, `RecipeSuggestions.razor`.
  - `Components/Account/Pages/**`: 31 stron Identity bez `_Imports.razor` (statyczne SSR, zgodnie z CLAUDE.md).
- Generycznych prymitywów (Button, Card, Badge, Alert, Spinner, FormField, EmptyState) jako komponentów Razor: **0**. Widoki składają je z klas Bootstrapa inline. Jedyne lokalne „mini-komponenty” to `RenderFragment` wewnątrz stron: `ProductSection` ([Products.razor:104](Components/Pages/Products.razor:104)) i `IngredientBadge` ([RecipeSuggestions.razor:110](Components/Pages/RecipeSuggestions.razor:110)).
- Bootstrap JS (`bootstrap.bundle.js`) jest vendorowany w `wwwroot/lib`, ale **nie jest ładowany** — `App.razor` ładuje tylko `blazor.web.js` i `PasskeySubmit.razor.js` ([App.razor:20-21](Components/App.razor:20)). Komponenty Bootstrapa wymagające JS (collapse, modal, dropdown, tooltip) nie działają bez dodania skryptu lub odtworzenia stanu w Blazorze.
- Ikony: brak zestawu ikon (Bootstrap Icons nie jest w repo). `NavMenu.razor.css` definiuje 5 klas ikon jako inline SVG data-URI ([NavMenu.razor.css:37-55](Components/Layout/NavMenu.razor.css:37)). Klasa `bi-list-nested-nav-menu` jest użyta w [NavMenu.razor:25](Components/Layout/NavMenu.razor:25) i [:30](Components/Layout/NavMenu.razor:30) (linki „Moje produkty”, „Przepisy”), ale nie ma reguły CSS (`git grep bi-list-nested` — tylko te 2 trafienia). Te dwa linki renderują pusty kwadrat 1.25rem zamiast ikony. Regułę `.bi-list-nested-nav-menu` usunął commit `fa7ad22` („remove the Blazor template sample pages”) razem z linkiem Weather, choć nowe linki nadal jej używają (zweryfikowane w `git show fa7ad22`).

### 4. Twarde kolory zamiast klas/tokenów rolowych

Zakres: 55 plików śledzonych przez git — `Components/**/*.razor`, `Components/**/*.css`, `wwwroot/app.css` (bez `wwwroot/lib`). Skan: `#hex`, `rgb(a)`, `hsl(a)`, `oklch`, nazwane kolory (`white`, `black`, `lightyellow`), klasy palety/stałego motywu (`bg-|text-|border-` + `white|black|light|dark|<kolor>`, `navbar-dark|light`) oraz `style="…"`.

W Bootstrapie nie istnieją klasy palety typu `bg-purple-500`; odpowiednikiem „twardego koloru” są literały w CSS i klasy o stałym motywie. Klasy `bg-primary`, `text-bg-warning`, `alert-danger`, `text-muted` są rolowe (odpowiedniki `bg-primary` / `text-muted-foreground`).

| Plik | Trafienia | Linie | Co to jest |
| --- | --- | --- | --- |
| `wwwroot/app.css` | 11 | 6, 10, 11, 12, 16, 28, 32, 36, 40, 42, 50 | Nadpisania linków, `.btn-primary`, focus ringu, walidacji, error boundary (`#b32121`, `white`), checkboxa — tabela w sekcji 2 |
| `Components/Layout/MainLayout.razor.css` | 5 | 12, 16, 17, 81, 83 | **Gradient sidebaru `rgb(5, 39, 103) → #3a0647`** (granat → fiolet, [:12](Components/Layout/MainLayout.razor.css:12)); top-row `#f7f7f7` / `#d6d5d5`; error UI `lightyellow`, cień `rgba(0,0,0,.2)` |
| `Components/Layout/NavMenu.razor.css` | 15 | 6, 10, 11, 15, 20, 38, 42, 46, 50, 54, 71, 83, 84, 88, 89 | Toggler i stany linków (`white`, `#d7d7d7`, `rgba(255,255,255,.1/.37/.5)`), top-row `rgba(0,0,0,.4)`, ikony SVG z `fill='white'` |
| `Components/Layout/ReconnectModal.razor.css` | 8 | 24, 30, 44, 93, 94, 100, 104, 115 | Modal `white`, backdrop, przycisk `#6b9ed2` / `#3b6ea2`, animacja `#0087ff` |
| `Components/Layout/NavMenu.razor` | 1 | 5 | `navbar-dark` — klasa stałego motywu (zakłada ciemne tło sidebaru) |

Pliki **bez** literałów kolorów: wszystkie 5 stron w `Components/Pages/` i wszystkie pliki w `Components/Account/**`. Jedyny `style="…"` w zakresie to `display: inline-block` w [TwoFactorAuthentication.razor:43](Components/Account/Pages/Manage/TwoFactorAuthentication.razor:43) — nie kolor.

Uwaga (z dokumentacji Bootstrapa, nie z repo): `.text-muted` jest od v5.3.0 oznaczony jako przestarzały na rzecz `.text-body-secondary`, który istnieje w vendorowanym CSS ([bootstrap.css:8572](wwwroot/lib/bootstrap/dist/css/bootstrap.css:8572)). Obie czytają `--bs-secondary-color`, więc to kwestia konwencji, nie tokenów.

### 5. Brakujące prymitywy UI dla widoku docelowego

**Widok docelowy (założenie):** `/recipes` — [RecipeSuggestions.razor](Components/Pages/RecipeSuggestions.razor). To ekran głównego flow (US-01), na który trafiają kolejne slice'y roadmapy: S-03 ranking, S-04 szczegóły, S-05 parametry posiłku ([roadmap.md:47-49](context/foundation/roadmap.md:47)).

**Dane, które widok pokazuje dziś** (`RecipeProposal`, [RecipeModels.cs:33-38](Recipes/RecipeModels.cs:33)): `Title`, `Summary?`, `PrepTimeMinutes?`, `Ingredients` (`Name`, `Amount?`, `Status` = Owned / AlwaysAtHome / Missing, [RecipeModels.cs:23-31](Recipes/RecipeModels.cs:23)), `Steps`.

**Dane, które widok musi pokazać według PRD:** „Masz X z Y składników” (bez „zawsze w domu”) + liczba brakujących składników + liczba użytych produktów „zużyj w pierwszej kolejności” ([prd.md:95](context/foundation/prd.md:95)); szczegóły przepisu (FR-006, [prd.md:69](context/foundation/prd.md:69)); parametry posiłku z małego zestawu (FR-004, [prd.md:65](context/foundation/prd.md:65); model `MealParameters(MealType, MaxPrepMinutes?, Servings)`, [RecipeModels.cs:15](Recipes/RecipeModels.cs:15)).

| Prymityw | Dlaczego potrzebny | Stan dziś | Dostępne w Bootstrapie |
| --- | --- | --- | --- |
| **StatusBadge** (rola → wariant) | 3 statusy składnika; ten sam `text-bg-warning` znaczy „Brakuje” w przepisach i „Sprawdź termin” w produktach | **Zrobione dla przepisów (S-04):** [IngredientStatusBadge.razor](Components/Ui/IngredientStatusBadge.razor). Nadal inline w [Products.razor:130](Components/Pages/Products.razor:130) („Sprawdź termin”) | `.badge` + `.text-bg-*` |
| **RecipeCard** | Każda propozycja; S-03 doda ocenę, S-04 link do szczegółów | **Zrobione (S-04):** [RecipeCard.razor](Components/Ui/RecipeCard.razor) | `.card` |
| **ScoreSummary / meter** | „Masz X z Y”, liczba braków, liczba produktów „zużyj najpierw” | **Zrobione (S-03):** [RecipeScoreSummary.razor](Components/Ui/RecipeScoreSummary.razor); wartości liczy `RecipeScore.From` w C#. `.progress` nadal nieużywany | `.progress` ([bootstrap.css:4958](wwwroot/lib/bootstrap/dist/css/bootstrap.css:4958)), nieużywany |
| **Alert z akcją / ErrorState** | Błąd wczytania i błąd generowania z „Spróbuj ponownie” | 3 kopie inline: [RecipeSuggestions.razor:22](Components/Pages/RecipeSuggestions.razor:22), [:47-50](Components/Pages/RecipeSuggestions.razor:47), [Products.razor:76](Components/Pages/Products.razor:76) | `.alert-danger` |
| **LoadingState: Spinner + Skeleton** | Generowanie trwa do minuty; 7-state matrix wymaga loading bez skoku layoutu | Tylko inline spinner [RecipeSuggestions.razor:37-42](Components/Pages/RecipeSuggestions.razor:37); skeleton brak | `.spinner-border`, `.placeholder` ([bootstrap.css:6790](wwwroot/lib/bootstrap/dist/css/bootstrap.css:6790)), nieużywany |
| **EmptyState** | Brak produktów → CTA do `/products` | Zwykły `<p>` z linkiem [RecipeSuggestions.razor:26-28](Components/Pages/RecipeSuggestions.razor:26); w produktach `<p class="text-muted">` [Products.razor:108](Components/Pages/Products.razor:108) | brak gotowego komponentu |
| **FormField** (label + input + walidacja) | Formularz parametrów posiłku (S-05) | Wzorzec `div.mb-3 > label + Input* + ValidationMessage` powtórzony 4× w [Products.razor:21-58](Components/Pages/Products.razor:21) | `.form-label`, `.form-control`, `.form-select` |
| **SegmentedControl / ToggleGroup** | Wybór `MealType` i limitu czasu z „małego zestawu” (FR-004) | Brak | `.btn-check` + `.btn-group` ([bootstrap.css:2490](wwwroot/lib/bootstrap/dist/css/bootstrap.css:2490)), działa bez JS |
| **Disclosure / szczegóły** | FR-006 / S-04 — rozwinięcie lub osobny widok przepisu | **Zrobione (S-04):** przełącznik „Pokaż/Ukryj przepis” na stanie Blazora w [RecipeCard.razor](Components/Ui/RecipeCard.razor); osobnego komponentu Disclosure brak | `.collapse` wymaga Bootstrap JS, który nie jest ładowany ([App.razor:20-21](Components/App.razor:20)) → `<details>` albo stan w Blazorze |
| **Ikona nawigacji** | Link „Przepisy” w menu | Klasa `bi-list-nested-nav-menu` bez reguły CSS ([NavMenu.razor:30](Components/Layout/NavMenu.razor:30)) | brak zestawu ikon w repo |

Stan pusty wyniku generowania jest już obsłużony w serwisie: 0 użytecznych propozycji zwraca `Failed` ([RecipeService.cs:62-65](Recipes/RecipeService.cs:62)), więc widok pokazuje alert, a nie pustą ramkę.

## Code References

- `Components/App.razor:9-11` — kolejność arkuszy: Bootstrap → `app.css` → bundel CSS isolation
- `Components/App.razor:20-21` — ładowane skrypty; brak Bootstrap JS
- `wwwroot/app.css:5-54` — nadpisania z literałami kolorów; `:54` jedyne użycie `var(--bs-*)` w kodzie aplikacji
- `wwwroot/lib/bootstrap/dist/css/bootstrap.css:7-126` — `:root, [data-bs-theme=light]`, 117 zmiennych
- `wwwroot/lib/bootstrap/dist/css/bootstrap.css:128-182` — `[data-bs-theme=dark]`, 52 zmienne
- `wwwroot/lib/bootstrap/dist/css/bootstrap.css:778-785` — breakpointy
- `wwwroot/lib/bootstrap/dist/css/bootstrap.css:3056-3069` — `.btn-primary` z literałami hex
- `Components/Layout/MainLayout.razor.css:12` — gradient sidebaru granat → fiolet
- `Components/Layout/NavMenu.razor:5,25,30` — `navbar-dark`; niezdefiniowana ikona `bi-list-nested-nav-menu`
- `Components/Pages/RecipeSuggestions.razor:22-91,110-115` — widok docelowy, inline karta/alert/spinner/badge
- `Recipes/RecipeModels.cs:15,23-38` — dane do wyświetlenia (parametry posiłku, status składnika, propozycja)
- `context/foundation/prd.md:65,69,95` — FR-004, FR-006, format oceny

## Architecture Insights

- Wariant kontraktu z `/10x-ui`: **istniejący design system** (Bootstrap 5.3.3) z tokenami w CSS variables, ale **bez warstwy komponentów w repo**. Według `/10x-ui` należy go rozszerzyć, a nie wprowadzać Tailwind/shadcn obok.
- Tokeny są w vendorowanym, skompilowanym pliku. Repo nie ma pipeline'u Sass, więc rebranding odbywa się przez nadpisanie zmiennych `--bs-*` w `:root` / `[data-bs-theme=dark]` w `app.css` **oraz** zmiennych komponentów (`--bs-btn-*`), bo `.btn-primary` nie czyta `--bs-primary`.
- Dryf pochodzi z szablonu `dotnet new blazor`: wszystkie literały kolorów w zakresie są w plikach szablonu (`app.css` i 3 `*.razor.css` w `Components/Layout/`). Strony feature'ów (`Products`, `RecipeSuggestions`) są czyste kolorystycznie, ale powielają prymitywy inline.
- Kod feature'ów jest zorganizowany w folderach z serwisami (`Pantry/`, `Recipes/`), ale nie ma odpowiednika dla UI. CLAUDE.md nie wskazuje katalogu współdzielonych komponentów ani zakazu literałów kolorów (brak reguły UI w [CLAUDE.md](CLAUDE.md)).

## Historical Context (from prior changes)

- `context/changes/first-recipe-generation/plan.md:5,45` — S-02 świadomie odłożył ocenę „Masz X z Y”, licznik braków, sortowanie i filtr ≤ 2 braków do S-03, a szczegóły do S-04. To wyjaśnia, dlaczego `RecipeProposal` nie ma pól oceny (stan zgodny z planem, nie luka).
- `context/archive/2026-10-03-pantry-add-products/plan.md:38` — plan S-01 kazał ponownie użyć istniejącej klasy ikony `bi-list-nested-nav-menu` zamiast dodawać nową. Commit `fa7ad22` usunął potem tę regułę jako „nieużywaną ikonę szablonu”, co spowodowało pustą ikonę w menu (sekcja 3). Decyzja z S-01: nadal słuszna (ponowne użycie), stan dziś: zepsuty.
- `context/archive/2026-10-03-pantry-add-products/plan.md:202` — wymóg S-01: wiersz z `IsExpiryDue` ma styl ostrzeżenia i badge „Sprawdź termin”; zrealizowany jako `list-group-item-warning` + `text-bg-warning` ([Products.razor:115](Components/Pages/Products.razor:115), [:131](Components/Pages/Products.razor:131)).
- W `context/archive/` (1 zarchiwizowana zmiana) nie ma badań stylów ani decyzji o design systemie.

## Related Research

Nie dotyczy — w `context/changes/**/` brak innego `research.md` o stylach.

## Open Questions

1. **Widok docelowy** — przyjęto `/recipes`. Jeśli docelowy jest `/products`, sekcja 5 zmienia się w: StatusBadge (termin ważności), ListItem produktu, FormField, EmptyState, ErrorState, a od S-06 akcje edycji/usuwania (potrzebny dialog potwierdzenia → Bootstrap JS albo komponent Blazor).
2. **Gdzie mają żyć komponenty** — np. `Components/Ui/` (namespace `KitchenAssistant.Components.Ui`). Decyzja dla `/10x-plan`; CLAUDE.md ostrzega przed kolizją nazw folderu z klasą strony.
3. **Tryb ciemny** — wymaga ustawienia `data-bs-theme` (np. na `<html>` w `App.razor`), a przy podążaniu za systemem — małego skryptu lub decyzji produktowej. PRD nie wspomina o trybie ciemnym (wyszukiwanie `ciemn|dark|motyw` w `context/foundation/prd.md` — 0 trafień), więc to decyzja do podjęcia, nie wymóg.
4. **Gradient sidebaru i literały szablonu** — zastąpić tokenami rolowymi (np. `--bs-primary` / własne `--ka-surface-nav`) czy zostawić jako motyw? Decyzja produktowa.
