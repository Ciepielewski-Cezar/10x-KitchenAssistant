# Account UI (`/Account/Manage` Profile) Implementation Plan

## Overview

Bring the Profile view (`/Account/Manage`) onto the app's design-system contract and trim it to the PRD. Five charges from [research.md](research.md) are in scope (C1–C5, see *Triage* there):

- C1: three Bootstrap literals re-pointed to tokens.
- C2: one label-above `FormField` component shared with Products.
- C3: the status alert variant chosen from an explicit kind instead of from the message text.
- C4: one h1 and a labelled account menu.
- C5: the view in Polish, with the phone and Username inputs removed and the menu cut to Profil, Hasło, Dane osobowe.

The change ends with a dev-only kitchen sink that shows the 7-state matrix, and a CLAUDE.md rule that keeps the next agent on `Components/Ui/` and `StatusKind`.

Phase order follows `/10x-ui`: token values → shared contract → one view → states and gate. There is no environment/library phase, because the change adds no dependency.

## Current State Analysis

- **The view is still the template.** `git log -- Components/Account` shows only the scaffold commit. [Index.razor:11-33](../../../Components/Account/Pages/Manage/Index.razor:11) renders `h3` "Profile", a disabled floating "Username" input, a floating phone input and a `w-100 btn btn-lg btn-primary` Save. No other code reads the phone (research §4).
- **Heading stack.** [ManageLayout.razor:4-8](../../../Components/Account/Shared/ManageLayout.razor:4) renders `h1` "Manage your account", `h2` "Change your account settings" and an `<hr>` above every Manage page.
- **Menu.** [ManageNavMenu.razor:6-31](../../../Components/Account/Shared/ManageNavMenu.razor:6) is a bare `ul.nav.nav-pills.flex-column` with seven English items, one of them conditional (External logins).
- **Status variant comes from the text.** [StatusMessage.razor:3](../../../Components/Account/Shared/StatusMessage.razor:3) picks `danger` when `DisplayMessage.StartsWith("Error")`. `IdentityRedirectManager` writes the raw message into the `Identity.StatusMessage` cookie ([IdentityRedirectManager.cs:40-51](../../../Components/Account/IdentityRedirectManager.cs:40)). `StatusMessage` has 24 call sites (17 with `Message=`) and there are 31 status redirects across `Components/Account/**`. No page redirects to `/Account/Manage` with a status, so once the form is gone Profile has no status messages of its own.
- **Tokens.** `app.css` already maps the theme onto `--bs-*`. Three Bootstrap literals still reach the view:
  - `.nav-link:focus-visible` has a blue ring ([bootstrap.css:3851-3854](../../../wwwroot/lib/bootstrap/dist/css/bootstrap.css:3851)).
  - `.form-floating > :disabled ~ label` is `#6c757d` ([bootstrap.css:2695-2698](../../../wwwroot/lib/bootstrap/dist/css/bootstrap.css:2695)).
  - `hr` is drawn in `currentColor` at 0.25 opacity ([bootstrap.css:209-215](../../../wwwroot/lib/bootstrap/dist/css/bootstrap.css:209)).
  The shared focus-ring rule is at [app.css:320-322](../../../wwwroot/app.css:320). `--border`, `--muted-foreground` and `--ring` exist in light and dark blocks ([app.css:61-68](../../../wwwroot/app.css:61), [:151-158](../../../wwwroot/app.css:151)).
- **No component layer.** `Components/` holds only `Account/`, `Layout/` and `Pages/`. Products builds label-above fields inline ([Products.razor:21-64](../../../Components/Pages/Products.razor:21)).
- **Language.** `<html lang="en">` ([App.razor:2](../../../Components/App.razor:2)), while product pages are Polish.
- **Tests.** `KitchenAssistant.Tests` is plain xUnit (no bUnit). `InternalsVisibleTo` is already set ([KitchenAssistant.csproj:27](../../../KitchenAssistant.csproj:27)), and existing tests use `internal static` helpers (e.g. `RecipeClassifier.Classify`).
- **Imports.** [Components/_Imports.razor](../../../Components/_Imports.razor) applies to every folder below it, including `Account/`. Account pages also import `KitchenAssistant.Components.Account.Shared` and carry `[ExcludeFromInteractiveRouting]` ([Account/Pages/_Imports.razor](../../../Components/Account/Pages/_Imports.razor)).

## Desired End State

A signed-in user who clicks their e-mail in the sidebar lands on a Polish page:

- An h1 reads "Moje konto".
- Next to it (or above it, on a phone) is a labelled menu with three pills: **Profil** (active), **Hasło**, **Dane osobowe**.
- Below the menu is an h2 "Profil" and one label-above field, **E-mail**, showing their address as read-only text. There is no form and no Save button.

The rest of the screen:

- **Keyboard focus:** tabbing the menu shows the same green `--ring` focus ring as buttons and inputs.
- **Phone width (375 px):** the three pills sit in one row above the content.
- **Language:** `<html lang="pl">`.

On Products, the four fields (Nazwa, Ilość, Data ważności, Miejsce przechowywania) render through the same `FormField`. Their look and validation behaviour are unchanged.

`StatusMessage` renders `alert-danger` for anything sent with `StatusKind.Error` and `alert-success` for `StatusKind.Success`, whatever the language. Callers that pass no kind keep today's inference from the "Error" prefix.

In Development, `/dev/ui` shows every state of the view's parts on one static SSR page; every other environment returns 404 there.

CLAUDE.md's UI section tells the next agent:
- shared components live in `Components/Ui/` (check there first)
- fields use `FormField`
- status messages pass an explicit `StatusKind`
- states are proved on `/dev/ui`

Verify with `dotnet build`, `dotnet test`, the hardcoded-value scan, and screenshots of `/Account/Manage`, `/products` and `/dev/ui` at desktop and 375 px.

### Key Discoveries:

- `app.css` loads after Bootstrap ([App.razor:9-10](../../../Components/App.razor:9)), so an equal-specificity rule there overrides Bootstrap. Adding `.nav-link:focus-visible` to the existing shared focus-ring selector ([app.css:320](../../../wwwroot/app.css:320)) is enough for C1's ring.
- `ValidationMessage` needs a cascading `EditContext`. The read-only e-mail on Profile sits outside any `EditForm`, so `FormField` must render `ValidationMessage` only when `For` is set.
- `Response.Cookies.Append` URL-encodes the value and `Request.Cookies` decodes it, so Polish characters and a kind prefix survive the status cookie unchanged.
- `FocusOnNavigate` targets `h1` ([Routes.razor:9](../../../Components/Routes.razor:9)). Keeping the h1 in `ManageLayout` keeps a target on every Manage page.
- .NET 10's `NavigationManager.NotFound()` works under static SSR and re-executes `/not-found` through `UseStatusCodePagesWithReExecute` ([Program.cs:97](../../../Program.cs:97)). That is how `/dev/ui` returns 404 outside Development.

## What We're NOT Doing

- Translating or restyling any other Account page: Login, Register, ChangePassword, PersonalData, Email, 2FA, Passkeys and so on keep their English copy and `form-floating` fields. The 24 other floating fields move to `FormField` in a later change (research *Triage*, deferred).
- Migrating the other 31 status redirects or 17 `Message=` call sites to an explicit `StatusKind`. They keep the legacy inference.
- Fixing [ConfirmEmailChange.razor:45](../../../Components/Account/Pages/ConfirmEmailChange.razor:45) (missing `$`, wrong variant). After Phase 2 it is a one-line fix in a later change.
- Removing routes. Email, Two-factor authentication and Passkeys stay reachable by URL; they only disappear from the menu.
- Main-nav changes: the entry label is still the raw e-mail ([NavMenu.razor:34-35](../../../Components/Layout/NavMenu.razor:34)), the main nav stays mixed-language, and the logout `ReturnUrl` loop stays.
- Removing `PhoneNumber` from the database. It is an Identity column; only the UI goes.
- Dark mode, a new CSS dependency, bUnit or Playwright.
- Adding an `Info`/neutral status kind. Two kinds cover this view; [Email.razor:89](../../../Components/Account/Pages/Manage/Email.razor:89) can add one when that page is migrated.

## Implementation Approach

Fix the contract before the pixels:

- **Phase 1** changes token mappings only, so its screenshots isolate CSS.
- **Phase 2** introduces the two shared pieces (`FormField`, `StatusKind`) and proves them on an existing caller (Products) and in unit tests, before the view depends on them.
- **Phase 3** rewrites the one view on top of that contract.
- **Phase 4** shows the states the trimmed view can no longer reach on its own (status error, field error, disabled) in a kitchen sink, then writes the guard rule.

Everything under `Components/Account/` stays static SSR. No `@rendermode` is added, and `/dev/ui` is static too, because `StatusMessage` reads `HttpContext`.

## Phase 1: Token re-points (C1)

### Overview

Point the three Bootstrap literals that reach the view at existing tokens. Edit `wwwroot/app.css` only.

### Changes Required:

#### 1. Nav-link focus ring

**File**: `wwwroot/app.css`

**Intent**: Keyboard focus on the account menu pills shows the same `--ring`-based ring as buttons and inputs, not Bootstrap blue.

**Contract**: Add `.nav-link:focus-visible` to the shared focus-ring selector list at `app.css:320` (`box-shadow: 0 0 0 0.1rem var(--background), 0 0 0 0.25rem var(--bs-focus-ring-color)`). Bootstrap's `outline: 0` on that selector stays.

#### 2. Disabled floating label

**File**: `wwwroot/app.css`

**Intent**: The disabled floating label follows the theme. It no longer appears on Profile after Phase 3, but 24 other Account fields still use floating labels.

**Contract**: New rule on `.form-floating > :disabled ~ label, .form-floating > .form-control:disabled ~ label` with `color: var(--muted-foreground)`, next to the existing `.form-floating` placeholder rules at the end of the file.

#### 3. Horizontal rule

**File**: `wwwroot/app.css`

**Intent**: `hr` draws in the border token instead of 25 % `currentColor`, so it follows a token change and dark values.

**Contract**: `hr { color: var(--border); opacity: 1; }`, placed with the other element-level mappings (after the `.list-group` block). Bootstrap's `border-top` stays.

### Success Criteria:

#### Automated Verification:

- `dotnet build` succeeds
- Hardcoded-value scan on `wwwroot/app.css` shows no new literal outside the existing derived-value block (compare `grep -cE '#[0-9a-fA-F]{3,8}\b|rgba?\(' wwwroot/app.css` before and after: count unchanged)

#### Manual Verification:

- On `/Account/Manage` (desktop), tabbing onto a menu pill shows the green ring, matching the focus ring on an input on `/products`
- On `/Account/Manage`, the layout `hr` (still present until Phase 3) is drawn in the `--border` colour
- On `/Account/Login` (or any page with a disabled floating field), nothing regresses visually

**Implementation Note**: After completing this phase and all automated verification passes, pause here for manual confirmation from the human that the manual testing was successful before proceeding to the next phase.

---

## Phase 2: Shared contract — `FormField` and `StatusKind` (C2, C3)

### Overview

Add the app's single field component and an explicit status kind. Prove the component on Products and the kind logic in unit tests, before Profile uses either.

### Changes Required:

#### 1. `FormField` component

**File**: `Components/Ui/FormField.razor` (new)

**Intent**: One label-above field primitive for the whole app, replacing the inline `div.mb-3 > label.form-label + input + ValidationMessage` markup. It is the first component in `Components/Ui/`.

**Contract**:
- Namespace `KitchenAssistant.Components.Ui`; a generic component with `@typeparam TValue`.
- Parameters:
  - `Id` (string, required): used as the label's `for`.
  - `Label` (string, required).
  - `ChildContent` (RenderFragment, required): the input itself, which keeps its own `id` and `class`.
  - `For` (`Expression<Func<TValue>>?`): when set, renders `<ValidationMessage For="For" />` after the input. When null, renders no validation, so the component works outside an `EditForm`.
- Renders `<div class="mb-3"><label for="@Id" class="form-label">@Label</label>@ChildContent …</div>`, with no literal colours and no scoped CSS.
- A caller that omits `For` passes `TValue="string"` explicitly.

#### 2. Import the Ui namespace

**File**: `Components/_Imports.razor`

**Intent**: `FormField` is available in every page, including Account pages, with no per-page `@using`.

**Contract**: Add `@using KitchenAssistant.Components.Ui`.

#### 3. Products adopts `FormField`

**File**: `Components/Pages/Products.razor`

**Intent**: Give `FormField` a real interactive caller with validation. Products' markup and behaviour stay the same.

**Contract**: The four `div.mb-3` blocks at lines 21-25, 42-46, 48-52 and 54-64 become `<FormField Id="…" Label="…" For="() => form.X">` wrapping the existing `InputText`, `InputDate` and `InputSelect`, with the same ids, labels, `@ref="nameInput"`, `ParsingErrorMessage` and options. The Kategoria `fieldset` (radio group) stays as it is.

#### 4. `StatusKind` and status-cookie helper

**File**: `Components/Account/StatusKind.cs` (new), `Components/Account/StatusMessageCookie.cs` (new)

**Intent**: The alert variant becomes data that travels with the message, not a property of the English text. The encode/decode/resolve logic lives in plain C# so it can be unit-tested.

**Contract**:
- `public enum StatusKind { Success, Error }` in namespace `KitchenAssistant.Components.Account`.
- `internal static class StatusMessageCookie`:
  - `string Encode(string message, StatusKind kind)` returns `"success|<message>"` or `"error|<message>"`.
  - `(string Message, StatusKind? Kind) Decode(string raw)`:
    - A recognised `success|` or `error|` prefix is stripped and yields its kind.
    - Anything else is a legacy value: the whole string is returned with `Kind = null`.
    - Only the first `|` separates, so a message containing `|` round-trips.
  - `StatusKind Resolve(string message, StatusKind? kind)`: an explicit kind wins. Otherwise `message.StartsWith("Error")` gives `Error`, and anything else gives `Success`. This is the legacy inference, kept in one place.

#### 5. Redirect manager overloads

**File**: `Components/Account/IdentityRedirectManager.cs`

**Intent**: New callers can send a kind. Existing callers compile and behave unchanged.

**Contract**:
- Add `RedirectToWithStatus(string uri, string message, StatusKind kind, HttpContext context)` and `RedirectToCurrentPageWithStatus(string message, StatusKind kind, HttpContext context)`. Both write `StatusMessageCookie.Encode(message, kind)` into the existing cookie, with the same builder.
- The existing overloads without a kind keep writing the raw message, so the legacy path still works.
- `RedirectToInvalidUser` stays as it is.

#### 6. `StatusMessage` reads the kind

**File**: `Components/Account/Shared/StatusMessage.razor`

**Intent**: Remove `StartsWith("Error")` from the component; it asks the helper for the variant instead.

**Contract**:
- New `[Parameter] public StatusKind? Kind { get; set; }`.
- When `Message` is set, display it with `Kind`.
- Otherwise display the decoded cookie message with its decoded kind.
- Variant: `StatusMessageCookie.Resolve(...) == StatusKind.Error ? "danger" : "success"`.
- Markup (`div.alert.alert-… role="alert"`) and cookie deletion stay unchanged.

#### 7. Unit tests

**File**: `KitchenAssistant.Tests/Account/StatusMessageCookieTests.cs` (new)

**Intent**: Pin the C3 behaviour, including the legacy fallback that 23 untouched pages depend on.

**Contract**: xUnit facts covering:
- `Encode` then `Decode` round-trips the message and kind for both kinds, using Polish text (e.g. "Nie udało się zapisać.", Error; "Zapisano zmiany.", Success).
- A message containing `|` round-trips.
- A legacy raw value ("Error: Failed to set phone number.") decodes to the whole text with `Kind = null`, and `Resolve` gives `Error`.
- A legacy raw value without the prefix ("Your profile has been updated") resolves to `Success`.
- An explicit kind beats the text: `Resolve("Error: x", StatusKind.Success)` gives `Success`, and `Resolve("Błąd zapisu", StatusKind.Error)` gives `Error`.
- An unknown prefix (`"info|x"`) is treated as legacy text.
- An empty string decodes without throwing.

### Success Criteria:

#### Automated Verification:

- `dotnet build` succeeds
- `dotnet test` passes, including `dotnet test --filter "FullyQualifiedName~StatusMessageCookieTests"`
- `grep -n 'StartsWith("Error")' Components/Account/Shared/StatusMessage.razor` returns no hits
- Hardcoded-value scan on `Components/Ui/FormField.razor` and `Components/Pages/Products.razor` returns 0 hits

#### Manual Verification:

- `/products` at desktop looks the same as before: labels above inputs, same spacing
- Submitting `/products` with an empty name still shows the validation message under Nazwa, and the field still gets the invalid outline
- After adding a product, focus still returns to Nazwa
- An existing Account flow with an English error (e.g. a wrong current password on `/Account/Manage/ChangePassword`) still renders `alert-danger`

**Implementation Note**: After completing this phase and all automated verification passes, pause here for manual confirmation from the human that the manual testing was successful before proceeding to the next phase.

---

## Phase 3: The Profile view (C4, C5)

### Overview

Rewrite the view on the contract: Polish copy, one h1, a labelled menu with three items that is horizontal on mobile, a read-only e-mail field, and the phone and Username code removed.

### Changes Required:

#### 1. Profile page

**File**: `Components/Account/Pages/Manage/Index.razor`

**Intent**: Profile shows only what the PRD's account model has: the e-mail. The phone field, which nothing else used, goes, along with the disabled Username that duplicated the e-mail.

**Contract**:
- `<PageTitle>Profil</PageTitle>`, then `<h2>Profil</h2>`, then `<StatusMessage />` (kept for consistency with the other Manage pages).
- Below them, inside the existing `row` / `col-xl-6`: one `<FormField TValue="string" Id="email" Label="E-mail">` wrapping `<input id="email" type="email" class="form-control-plaintext" value="@email" readonly />`.
- Code: inject `UserManager` and `IdentityRedirectManager` only. `OnInitializedAsync` loads the user (with `RedirectToInvalidUser` on null, as today) and reads `GetEmailAsync`.
- Remove `EditForm`, `InputModel`, `OnValidSubmitAsync`, `SignInManager`, the phone fields and the `System.ComponentModel.DataAnnotations` using. The `@page "/Account/Manage"` route stays.

#### 2. Manage layout

**File**: `Components/Account/Shared/ManageLayout.razor`

**Intent**: One page-title heading instead of h1 + h2 + hr, in line with Products and Recipes.

**Contract**:
- `<h1>Moje konto</h1>` followed by the existing `row` with `col-lg-3` (menu) and `col-lg-9` (`@Body`).
- Remove the `h2`, the `<hr>` and the wrapper `div`.
- Below `lg` the menu column needs bottom spacing (`mb-3 mb-lg-0` on the menu column).
- Still `@layout MainLayout`, and still no `@rendermode`.

#### 3. Manage menu

**File**: `Components/Account/Shared/ManageNavMenu.razor`

**Intent**: A named navigation landmark with only the PRD-relevant items, readable at phone width.

**Contract**:
- `<nav aria-label="Ustawienia konta">` wrapping `<ul class="nav nav-pills flex-lg-column">`. That is horizontal below `lg` and a column from `lg`.
- Items, in order:
  - **Profil** (`Account/Manage`, `NavLinkMatch.All`)
  - **Hasło** (`Account/Manage/ChangePassword`)
  - the existing `hasExternalLogins` conditional, labelled **Logowanie zewnętrzne**
  - **Dane osobowe** (`Account/Manage/PersonalData`)
- Remove the Email, Two-factor authentication and Passkeys items. Their routes remain.

#### 4. Document language

**File**: `Components/App.razor`

**Intent**: Declare the app's language as Polish.

**Contract**: `<html lang="pl">`. This is global: Login and Register stay English until translated (accepted in research *Triage*).

### Success Criteria:

#### Automated Verification:

- `dotnet build` succeeds
- `dotnet test` passes
- Hardcoded-value scan on `Index.razor`, `ManageLayout.razor`, `ManageNavMenu.razor`, `StatusMessage.razor` returns 0 hits
- `grep -nE 'PhoneNumber|form-floating|Manage your account|Two-factor|Passkeys' Components/Account/Pages/Manage/Index.razor Components/Account/Shared/ManageLayout.razor Components/Account/Shared/ManageNavMenu.razor` returns no hits
- `grep -n 'lang="pl"' Components/App.razor` returns one hit

#### Manual Verification:

- Signed in as a dev test user, `/Account/Manage` at desktop shows the h1 "Moje konto", the menu column with Profil active, the h2 "Profil" and the E-mail field showing the user's address as text, with no Save button and no English strings in the view
- At 375 px the three pills sit in one row above the content, and the content starts directly below them
- Tabbing the page reaches Profil → Hasło → Dane osobowe in that order, each with the green focus ring
- The menu is announced as the navigation "Ustawienia konta" (accessibility tree via `read_page`)
- `/Account/Manage/Email`, `/Account/Manage/TwoFactorAuthentication` and `/Account/Manage/Passkeys` still load by URL
- Clicking Hasło and Dane osobowe opens those (English) pages under the new layout without a layout break

**Implementation Note**: After completing this phase and all automated verification passes, pause here for manual confirmation from the human that the manual testing was successful before proceeding to the next phase.

---

## Phase 4: States, visual gate and guard

### Overview

Add a dev-only kitchen sink that renders every state of the view's parts at once, screenshot it with the real pages, and write the CLAUDE.md rule that keeps the contract.

### Changes Required:

#### 1. Kitchen sink page

**File**: `Components/Pages/DevUi.razor` (new)

**Intent**: Show the 7-state matrix in one place, including the states trimmed Profile can no longer reach (status error, field error, disabled). It exists only in Development.

**Contract**:
- Route `@page "/dev/ui"`, static SSR (no `@rendermode`), not linked from any menu, `@using KitchenAssistant.Components.Account.Shared`.
- Injects `IWebHostEnvironment` and `NavigationManager`. In `OnInitialized`, when not Development, it calls `NavigationManager.NotFound()` and renders nothing.
- Sections, each under an h2 naming the state:
  1. **FormField:** default (`InputText`), read-only (`form-control-plaintext`, as on Profile), disabled (`disabled` input), and error. For the error case, an `EditForm` over a small model with a `[Required]` property and an `EditContext` validated in `OnInitialized`, so the `ValidationMessage` and `.invalid` outline render server-side.
  2. **Buttons:** `btn btn-primary`, plus the same button `disabled`.
  3. **StatusMessage:** `Kind="StatusKind.Success"` with Polish text, `Kind="StatusKind.Error"` with Polish text, and a legacy `Message="Error: …"` with no kind (must render danger).
  4. **ManageNavMenu:** the real component.
- No literal colours, no scoped CSS.

#### 2. Guard rule

**File**: `CLAUDE.md` (UI section, outside the `<!-- BEGIN @przeprogramowani/10x-cli -->` block)

**Intent**: The next agent finds the components and the status contract instead of copying template markup.

**Contract**: Rewrite the "Shared components" bullet (`CLAUDE.md:40`) to say:
- Shared UI components live in `Components/Ui/` (namespace `KitchenAssistant.Components.Ui`, imported globally), so check there before creating one.
- Form fields use `FormField` (label-above), not `form-floating`; the Account pages still on floating labels are legacy.
- New `StatusMessage` / `RedirectTo…WithStatus` callers pass an explicit `StatusKind`, never rely on an "Error" text prefix.
- States are proved on the Development-only `/dev/ui` kitchen sink, which a new shared component must be added to.

Keep the pointer to `recipes-ui/research.md` §5 for the remaining candidates.

### Success Criteria:

#### Automated Verification:

- `dotnet build` succeeds
- `dotnet test` passes
- Hardcoded-value scan on every `.razor` file touched by this change (`Index.razor`, `ManageLayout.razor`, `ManageNavMenu.razor`, `StatusMessage.razor`, `FormField.razor`, `Products.razor`, `DevUi.razor`, `App.razor`) returns 0 hits
- `grep -n 'Components/Ui' CLAUDE.md` and `grep -n 'StatusKind' CLAUDE.md` each return a hit above the `BEGIN @przeprogramowani/10x-cli` marker line

#### Manual Verification:

- `/dev/ui` in Development, screenshotted at desktop and 375 px, shows every matrix cell:
  - **default:** all FormField variants and the menu, built from tokens
  - **hover:** pills and button change on hover via token colours
  - **focus-visible:** tabbing shows the green ring on input, button and pills
  - **disabled:** the disabled input and button look disabled and stay readable
  - **error:** the field error message sits under the field with the invalid outline; the Error StatusMessage and the legacy "Error:" message render `alert-danger`, and the Success one renders `alert-success`
- The two cells that cannot be shown are recorded in the phase notes as N/A with a reason:
  - **empty:** Profile has no list or data region, and a signed-in user always has an e-mail.
  - **loading:** the page is static SSR and renders complete on the server, with no in-page async region; interactivity is forbidden under `Components/Account/`.
- `/Account/Manage` and `/products` screenshots at desktop and 375 px match Phase 2 and Phase 3 (no regression from adding the sink)
- With the app run in Production (e.g. `./scripts/local-prod.ps1`), `/dev/ui` returns the not-found page
- The 7-state matrix and the deferred charges are written into the change folder (a short section appended to `research.md` *Triage* or a `states.md`) together with the screenshot file names

**Implementation Note**: After completing this phase and all automated verification passes, pause here for manual confirmation from the human that the manual testing was successful before proceeding to the next phase.

---

## Testing Strategy

### Unit Tests:

- `StatusMessageCookieTests` (Phase 2): encode/decode round-trip for both kinds with Polish text, `|` inside the message, legacy raw values with and without the "Error" prefix, explicit kind overriding the text, unknown prefix, empty value.

### Integration Tests:

- None. Markup is verified with the kitchen sink and screenshots (agreed: no bUnit/Playwright dependency).

### Manual Testing Steps:

1. Run `dotnet watch` and sign in with a dev test account on LocalDB.
2. Open `/Account/Manage` at desktop and at 375 px. Check the heading, menu, e-mail field and Polish copy, then Tab through the menu.
3. Open `/products`. Submit an empty form, then add a product, and confirm validation and focus behave as before.
4. Open `/Account/Manage/ChangePassword` and submit a wrong current password: still `alert-danger` (legacy path).
5. Open `/dev/ui` and walk the matrix at both widths.
6. Run the production-like stack and confirm `/dev/ui` is not found.

## Performance Considerations

None. The Profile page loses a form and a phone lookup; the status cookie gains a short prefix.

## Migration Notes

No database change. `AspNetUsers.PhoneNumber` stays, unused. Status cookies written by an older build during deploy (5 s max age) decode as legacy values and keep working.

## References

- Research and triage: `context/changes/account-ui/research.md`
- Change identity: `context/changes/account-ui/change.md`
- Token source and mapping rules: `wwwroot/app.css`, `context/changes/recipes-ui/research.md` (§5 component candidates)
- Field pattern being extracted: `Components/Pages/Products.razor:21-64`
- UI contract skill: `.claude/skills/10x-ui/SKILL.md` (7-state matrix, visual gate, guard rule)

## Progress

> Convention: `- [ ]` pending, `- [x]` done. Append ` — <commit sha>` when a step lands. Do not rename step titles. See `references/progress-format.md`.

### Phase 1: Token re-points (C1)

#### Automated

- [x] 1.1 `dotnet build` succeeds — 30c03c2
- [x] 1.2 Hardcoded-value scan on `wwwroot/app.css` shows no new literal outside the existing derived-value block — 30c03c2

#### Manual

- [x] 1.3 Menu pill focus shows the green ring, matching inputs on `/products` — 30c03c2
- [x] 1.4 Layout `hr` is drawn in the `--border` colour — 30c03c2
- [x] 1.5 No visual regression on a page with a disabled floating field — 30c03c2

### Phase 2: Shared contract — `FormField` and `StatusKind` (C2, C3)

#### Automated

- [x] 2.1 `dotnet build` succeeds
- [x] 2.2 `dotnet test` passes, including `StatusMessageCookieTests`
- [x] 2.3 `StatusMessage.razor` no longer contains `StartsWith("Error")`
- [x] 2.4 Hardcoded-value scan on `FormField.razor` and `Products.razor` returns 0 hits

#### Manual

- [x] 2.5 `/products` at desktop looks the same as before
- [x] 2.6 Empty-name submit on `/products` still shows the validation message and invalid outline
- [x] 2.7 After adding a product, focus returns to Nazwa
- [x] 2.8 An English Account error (wrong current password) still renders `alert-danger`

### Phase 3: The Profile view (C4, C5)

#### Automated

- [ ] 3.1 `dotnet build` succeeds
- [ ] 3.2 `dotnet test` passes
- [ ] 3.3 Hardcoded-value scan on the four view files returns 0 hits
- [ ] 3.4 No `PhoneNumber`, `form-floating`, English layout heading, Two-factor or Passkeys strings left in the view files
- [ ] 3.5 `App.razor` declares `lang="pl"`

#### Manual

- [ ] 3.6 `/Account/Manage` at desktop shows h1 "Moje konto", menu with Profil active, h2 "Profil" and the read-only E-mail field, all in Polish
- [ ] 3.7 At 375 px the three pills sit in one row above the content
- [ ] 3.8 Tab order Profil → Hasło → Dane osobowe, each with the green focus ring
- [ ] 3.9 Menu is exposed as navigation "Ustawienia konta"
- [ ] 3.10 Email, TwoFactorAuthentication and Passkeys routes still load by URL
- [ ] 3.11 Hasło and Dane osobowe open under the new layout without a layout break

### Phase 4: States, visual gate and guard

#### Automated

- [ ] 4.1 `dotnet build` succeeds
- [ ] 4.2 `dotnet test` passes
- [ ] 4.3 Hardcoded-value scan on every `.razor` file touched by this change returns 0 hits
- [ ] 4.4 CLAUDE.md names `Components/Ui` and `StatusKind` above the 10x-cli marker

#### Manual

- [ ] 4.5 `/dev/ui` at desktop and 375 px shows default, hover, focus-visible, disabled and error cells
- [ ] 4.6 Empty and loading recorded as N/A with reasons
- [ ] 4.7 `/Account/Manage` and `/products` screenshots at both widths show no regression
- [ ] 4.8 `/dev/ui` returns not-found in Production
- [ ] 4.9 7-state matrix, deferred charges and screenshot names recorded in the change folder
