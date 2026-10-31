---
date: 2026-10-06T21:24:56+02:00
researcher: Claude (Opus 5.5) for Cezar Ciepielewski
git_commit: df44e65
branch: main
repository: Ciepielewski-Cezar/10x-KitchenAssistant
topic: "UI audit of /Account/Manage (Profile): charges for the design-system contract"
tags: [research, ui, account, identity, bootstrap, design-tokens, 10x-ui]
status: complete
last_updated: 2026-10-06
last_updated_by: Claude (Opus 5.5)
last_updated_note: "Added Triage: C1–C5 in scope (C5 trimmed to PRD, view translated to Polish), deferrals recorded"
---

# Research: UI audit of `/Account/Manage`

**Date**: 2026-10-06T21:24:56+02:00
**Researcher**: Claude (Opus 5.5) for Cezar Ciepielewski
**Git Commit**: df44e65 (uncommitted changes exist only under `.claude/` and in this change folder; `git status` shows no local edits under `Components/` or `wwwroot/`)
**Branch**: main
**Repository**: Ciepielewski-Cezar/10x-KitchenAssistant

## Research Question

Run the `/10x-ui` two-way audit on the view named in [change.md](change.md):
`/Account/Manage` ([Index.razor](../../../Components/Account/Pages/Manage/Index.razor)) with
[ManageLayout.razor](../../../Components/Account/Shared/ManageLayout.razor),
[ManageNavMenu.razor](../../../Components/Account/Shared/ManageNavMenu.razor) and
[StatusMessage.razor](../../../Components/Account/Shared/StatusMessage.razor), against the token source
`wwwroot/app.css`. Produce 3–5 charges (missing tokens, missing shared component, accidental
architecture), each with file:line and the effect on the user, plus a first read of the 7-state matrix.
Constraint: `Components/Account/` stays static SSR.

## Summary

The view has **no colour literals of its own**: the hardcoded-value scan on the 4 view files returns 0
hits, and across all `Components/Account/**/*.razor` it returns 1 hit, `style="display: inline-block"` at
[TwoFactorAuthentication.razor:43](../../../Components/Account/Pages/Manage/TwoFactorAuthentication.razor:43) (not a colour).
The theme from `recipes-ui` already reaches most of the view through `--bs-*` mappings
([app.css:212-243](../../../wwwroot/app.css:212)), `.btn-primary` ([:246-260](../../../wwwroot/app.css:246)),
`.nav-pills` ([:297-300](../../../wwwroot/app.css:297)) and the focus ring for `.btn`/`.form-control`
([:320-326](../../../wwwroot/app.css:320)).

The problems are elsewhere. Account files are untouched since the `dotnet new blazor --auth Individual`
scaffold (`git log -- Components/Account` shows only `b7d9e0a`), so the view is the template in the
product's colours: English copy in a Polish app, a three-heading stack before any content, fields the
product never uses, and a status alert that picks its colour from the message text. Three Bootstrap
values the theme does not re-point still reach this view.

### Charges

| # | Category | Evidence (file:line) | Effect on the user |
| --- | --- | --- | --- |
| C1 | Missing tokens | `.nav-link:focus-visible` ring is literal `rgba(13,110,253,.25)` ([bootstrap.css:3851-3854](../../../wwwroot/lib/bootstrap/dist/css/bootstrap.css:3851)); app.css re-points only `.btn-link.nav-link:focus` ([app.css:320](../../../wwwroot/app.css:320)). Also the disabled floating label `color: #6c757d` ([bootstrap.css:2695-2698](../../../wwwroot/lib/bootstrap/dist/css/bootstrap.css:2695)) on Username ([Index.razor:22-23](../../../Components/Account/Pages/Manage/Index.razor:22)), and `hr` drawn as `currentColor` at 25 % opacity instead of `--border` ([bootstrap.css:209-215](../../../wwwroot/lib/bootstrap/dist/css/bootstrap.css:209), used at [ManageLayout.razor:8](../../../Components/Account/Shared/ManageLayout.razor:8)) | A keyboard user tabbing the account menu gets a Bootstrap-blue ring, while the button and inputs on the same page get the green `--ring`. The disabled label grey and the divider do not follow a token change, and would not follow dark mode. |
| C2 | Missing shared component | Floating-label field `div.form-floating.mb-3 > input + label + ValidationMessage` ([Index.razor:21-29](../../../Components/Account/Pages/Manage/Index.razor:21)): 26 copies in 15 Account pages. The product form uses label-above `div.mb-3 > label.form-label + Input*` ([Products.razor:21-25](../../../Components/Pages/Products.razor:21)). Submit `w-100 btn btn-lg btn-primary` ([Index.razor:30](../../../Components/Account/Pages/Manage/Index.razor:30)) appears on 14 pages; the product submit is `btn btn-primary` ([Products.razor:66](../../../Components/Pages/Products.razor:66)) | The same app has two field styles and two primary-button sizes. Moving from "Moje produkty" to the profile looks like changing applications. The floating placeholder is right-aligned until focus ([app.css:362-369](../../../wwwroot/app.css:362)), which nothing else in the app does. |
| C3 | Accidental architecture | `StatusMessage` picks `alert-danger` vs `alert-success` with `DisplayMessage.StartsWith("Error")` ([StatusMessage.razor:3](../../../Components/Account/Shared/StatusMessage.razor:3)). Error text without that prefix renders green, e.g. [ConfirmEmailChange.razor:45](../../../Components/Account/Pages/ConfirmEmailChange.razor:45) (which also lacks `$`, so `{userId}` prints literally); neutral text renders green too, e.g. [Email.razor:89](../../../Components/Account/Pages/Manage/Email.razor:89) | An error can look like a success. Translating the messages to Polish ("Błąd: …") would turn every error on the Account pages green, because the alert variant is encoded in English message text instead of a status value. |
| C4 | Accidental architecture | [ManageLayout.razor:4-8](../../../Components/Account/Shared/ManageLayout.razor:4) renders `h1` "Manage your account", `h2` "Change your account settings" and `<hr>` above every Manage page; the page title is an `h3` ([Index.razor:13](../../../Components/Account/Pages/Manage/Index.razor:13)). The section menu is a bare `<ul class="nav nav-pills flex-column">` ([ManageNavMenu.razor:6](../../../Components/Account/Shared/ManageNavMenu.razor:6)) in `col-lg-3` ([ManageLayout.razor:10](../../../Components/Account/Shared/ManageLayout.razor:10)) | The user sees three headings of close size (2.5 / 2 / 1.75 rem at ≥1200 px, [bootstrap.css:225-249](../../../wwwroot/lib/bootstrap/dist/css/bootstrap.css:225)) before the form. Below 992 px the six menu pills stack above the form, so on a phone the form starts below brand bar, "About" bar, h1, h2, hr and six links (inferred from markup, not rendered). Product pages use one `h1` as page title ([Products.razor:16](../../../Components/Pages/Products.razor:16), [RecipeSuggestions.razor:18](../../../Components/Pages/RecipeSuggestions.razor:18)). Screen readers get no landmark for the section menu. |
| C5 | Accidental architecture | Template scope, not product scope. Entry link label is the raw e-mail ([NavMenu.razor:34-35](../../../Components/Layout/NavMenu.razor:34)); the page is English in a Polish UI with `<html lang="en">` ([App.razor:2](../../../Components/App.razor:2)); "Username" is disabled and equals the e-mail ([Register.razor:77](../../../Components/Account/Pages/Register.razor:77)) but its placeholder says "Choose your username." ([Index.razor:22](../../../Components/Account/Pages/Manage/Index.razor:22)); the only editable field is a phone number that no other code reads; the menu offers Email change, whose confirmation is never sent ([Email.razor:108](../../../Components/Account/Pages/Manage/Email.razor:108) with `IdentityNoOpEmailSender`, [Program.cs:49](../../../Program.cs:49)) | A user who clicks their e-mail in the sidebar lands on an English page whose one action is saving a phone number the app never uses. From there the "Email" item tells them a confirmation link was sent, and none arrives. |

Charges C1–C2 are restyle work inside the existing contract. C3–C5 are entry-point and content decisions;
C5 in particular needs product choices before `/10x-plan` (see *Open Questions*, resolved in *Triage*).

## Triage

Decided by Cezar Ciepielewski on 2026-10-06. C1 and C4 needed no product choice; the others were answered
in the triage questions.

| Charge | Verdict | Scope in this change |
| --- | --- | --- |
| C1 Missing tokens | **in** | Re-point `.nav-link:focus-visible`, the disabled floating label (it disappears anyway if Username becomes text, see C5) and `hr` to tokens in `app.css`. |
| C2 Missing shared component | **in (this view)** | Label-above is the app's field style. Extract `FormField` into `Components/Ui/` (namespace `KitchenAssistant.Components.Ui`) and use it on the Profile form. Products' 4 fields may adopt it in the same phase, at the plan's discretion. |
| C3 StatusMessage variant | **in** | Required by the translation: the variant must no longer come from `StartsWith("Error")`. Mechanism (explicit parameter, separate cookie value, or prefix convention) is a plan decision; this view's two messages ([Index.razor:76](../../../Components/Account/Pages/Manage/Index.razor:76), [:82](../../../Components/Account/Pages/Manage/Index.razor:82)) must render with the right variant in Polish. |
| C4 Heading stack, menu landmark | **in** | One page-title heading per page in line with Products/Recipes, the layout's h1/h2/hr reduced, and `ManageNavMenu` wrapped in a labelled `<nav>`. Mobile order (menu above form) is a plan decision. |
| C5 Template content | **in, trimmed to PRD** | Translate the Profile page, `ManageLayout`, `ManageNavMenu` and this view's status strings to Polish; set `<html lang="pl">` ([App.razor:2](../../../Components/App.razor:2)). Profile shows the e-mail as read-only text: phone field and "Username" input removed. Menu keeps Profile, Password, Personal data; Email, Two-factor authentication and Passkeys are hidden (External logins is already hidden). |

Consequences the plan must carry:

- The menu labels become Polish, but the Password and Personal data pages behind them stay English (only
  this view is translated). Hidden items stay reachable by URL; no route is removed.
- `lang="pl"` is global, so Login/Register and other English Account pages will be declared Polish until
  they are translated.
- Removing the phone input leaves `OnValidSubmitAsync` with nothing to save; the Profile form (and its
  Save button) may disappear entirely, which changes the 7-state matrix for this view (disabled/error
  may become N/A).

**Deferred** (with reason):

- **Floating labels elsewhere:** the other 24 `form-floating` fields in Account pages (26 minus the 2 on
  this view) move to `FormField` in a later change. Reason: one view per change.
- **Translating the rest of Account:** Login, Register, Password, Personal data, etc. Reason: one view per change; C3 makes it safe to do later.
- **Main-nav entry label** (raw e-mail, [NavMenu.razor:34-35](../../../Components/Layout/NavMenu.razor:34)) and the
  mixed-language main nav. Reason: outside this view; belongs with the nav/i18n change.
- **`ConfirmEmailChange.razor:45`** missing `$` and wrong variant. Reason: another page. C3's mechanism should make it fixable in one line later.
- **Logout `ReturnUrl` loop** (§3). Reason: a `NavMenu`/endpoint bug, not this view.
- **Kitchen sink:** not a charge. The visual gate still needs a route that can render this view's states
  under static SSR; the plan decides its form.

## Detailed Findings

### 1. Source → view: what the view reads

CSS order is `bootstrap.min.css` → `app.css` → scoped bundle ([App.razor:9-11](../../../Components/App.razor:9)),
so an `app.css` rule beats a Bootstrap rule of equal specificity.

| Used in the view | Bootstrap reads | Covered by app.css |
| --- | --- | --- |
| `.nav-link` colour, hover | `--bs-link-color`, `--bs-link-hover-color` ([bootstrap.css:3818-3850](../../../wwwroot/lib/bootstrap/dist/css/bootstrap.css:3818)) | yes, [app.css:121-124](../../../wwwroot/app.css:121) |
| `.nav-link:focus-visible` | literal blue ring ([bootstrap.css:3851-3854](../../../wwwroot/lib/bootstrap/dist/css/bootstrap.css:3851)) | **no** (C1) |
| `.nav-pills` active | literal `#fff` / `#0d6efd` ([bootstrap.css:3893-3897](../../../wwwroot/lib/bootstrap/dist/css/bootstrap.css:3893)) | yes, [app.css:297-300](../../../wwwroot/app.css:297) |
| `.form-control`, `:disabled` bg | `--bs-body-*`, `--bs-border-color`, `--bs-secondary-bg` | yes, [app.css:220-226](../../../wwwroot/app.css:220) |
| `.form-control:focus` | literal `#86b7fe` + blue shadow ([bootstrap.css:2143-2149](../../../wwwroot/lib/bootstrap/dist/css/bootstrap.css:2143)) | yes, [app.css:320-326](../../../wwwroot/app.css:320) |
| `.form-floating > :disabled ~ label` | literal `#6c757d` ([bootstrap.css:2695-2698](../../../wwwroot/lib/bootstrap/dist/css/bootstrap.css:2695)) | **no** (C1) |
| `.form-floating > label` (filled), `::after` | `--bs-body-color-rgb`, `--bs-body-bg` | yes, [app.css:116](../../../wwwroot/app.css:116), [:220](../../../wwwroot/app.css:220) |
| `.alert-success`, `.alert-danger` | `--bs-{success,danger}-text-emphasis/-bg-subtle/-border-subtle` | yes, [app.css:104-111](../../../wwwroot/app.css:104) |
| `.btn-primary`, `.btn-lg` | `--bs-btn-*`, `--bs-border-radius-lg` | yes, [app.css:246-260](../../../wwwroot/app.css:246), [:238](../../../wwwroot/app.css:238) |
| `.text-danger`, `.validation-message`, `.invalid` | `--bs-danger-rgb`; app rules on `--bs-form-invalid-color` | yes, [app.css:108](../../../wwwroot/app.css:108), [:340-346](../../../wwwroot/app.css:340) |
| `hr` | `currentColor`, opacity .25 ([bootstrap.css:209-215](../../../wwwroot/lib/bootstrap/dist/css/bootstrap.css:209)) | not a literal, but ignores `--border` (C1) |
| `h1`–`h3` | `--bs-heading-color: inherit` → `--foreground`; font via `--bs-font-sans-serif` → `--font-sans` ([app.css:233](../../../wwwroot/app.css:233)) | yes; app.css sets no heading sizes |

The three literals in the C1 rows are also present in `bootstrap.min.css` (checked with grep on the
minified file: `.nav-link:focus-visible{…rgba(13,110,253,.25)}`, `.form-floating>…:disabled~label{color:#6c757d}`).

Side note, not a charge: an invalid field gets both the `.invalid` outline ([app.css:340-342](../../../wwwroot/app.css:340))
and, on focus, the green `--ring` border ([app.css:324-326](../../../wwwroot/app.css:324)), so a focused invalid field
shows a red outline around a green border (inferred from the cascade, not rendered).

### 2. View → source: duplication and missing components

There is no shared UI component folder: `Components/` holds only `Account/`, `Layout/` and `Pages/`, and
the `recipes-ui` §5 candidates have not been extracted (that change touched only `app.css`, fonts, layout CSS
and `NavMenu.razor`, commit `2ab284a`). Counts across `Components/Account/**` (14 Manage pages + top-level Account pages):

- `form-floating` field: 26 occurrences in 15 pages (13 in 7 Manage pages, 13 in 8 top-level pages).
- `w-100 btn btn-lg btn-primary`: 14 pages (6 Manage, 8 top-level).
- `ValidationSummary class="text-danger" role="alert"`: 15 pages (7 Manage, 8 top-level).
- Page heading + `<StatusMessage />`: 12 of 14 Manage pages, in two orders (h3 first on 5, StatusMessage first on 7); `RenamePasskey` uses `h4`, `GenerateRecoveryCodes` has no StatusMessage.

`StatusMessage` is the only alert component in the repo, but it is Identity-specific: it reads and
deletes the `IdentityRedirectManager.StatusCookieName` cookie ([StatusMessage.razor:20-28](../../../Components/Account/Shared/StatusMessage.razor:20)).
Product pages write alerts inline ([Products.razor:76](../../../Components/Pages/Products.razor:76),
[RecipeSuggestions.razor:22](../../../Components/Pages/RecipeSuggestions.razor:22), [:47](../../../Components/Pages/RecipeSuggestions.razor:47)).
CLAUDE.md asks for extraction when markup repeats on a second page and leaves the folder location open.

StatusMessage call sites (C3): among the inspected `Components/Account/**` messages, those meant as errors
start with "Error" except `ConfirmEmailChange.razor:45`. Messages that are neutral or wrong but render
green: [Email.razor:89](../../../Components/Account/Pages/Manage/Email.razor:89) "Your email is unchanged.",
[Email.razor:108](../../../Components/Account/Pages/Manage/Email.razor:108) (no-op sender, claims a link was sent).

Blazor's `ValidationMessage`/`ValidationSummary` with `class="text-danger"` most likely replace the default
`validation-message` class on the outer element (framework behaviour recalled, not verified at runtime);
both resolve to the destructive colour, so the visible result is the same.

### 3. Entry points and logged-out access

- The one link to `/Account/Manage` outside the section is in the main sidebar, labelled with
  `@context.User.Identity?.Name` ([NavMenu.razor:34-35](../../../Components/Layout/NavMenu.razor:34)), which is the
  e-mail because Register sets `UserName = Email` ([Register.razor:77](../../../Components/Account/Pages/Register.razor:77)).
  Home links only to products and recipes. Logged-out users see Register/Login instead.
- Logged out: `[Authorize]` on the folder ([Manage/_Imports.razor:2](../../../Components/Account/Pages/Manage/_Imports.razor:2))
  plus `[ExcludeFromInteractiveRouting]` ([Pages/_Imports.razor:2](../../../Components/Account/Pages/_Imports.razor:2)) make it a static SSR
  endpoint; no `LoginPath` is configured (Program.cs calls only `.AddIdentityCookies()`, [Program.cs:27](../../../Program.cs:27)),
  so the Identity default `/Account/Login` applies. `ReturnUrl` is honoured by Login. The router fallback
  is `AuthorizeRouteView` → `RedirectToLogin` ([Routes.razor:4-6](../../../Components/Routes.razor:4)). Traced from code, not run.
  No raw JSON or blank-frame entry was found.
- Out-of-view finding: logout posts `ReturnUrl=@currentUrl` and the endpoint does `LocalRedirect($"~/{returnUrl}")`
  ([IdentityComponentsEndpointRouteBuilderExtensions.cs:44-51](../../../Components/Account/IdentityComponentsEndpointRouteBuilderExtensions.cs:44)).
  Logging out from `/Account/Manage`, `/products` or `/recipes` returns to a protected page, which then
  challenges to Login instead of showing Home. Belongs to `NavMenu`, not this view.

### 4. Content against the PRD (C5)

- PRD scope for accounts: FR-001 "Użytkownik może założyć konto i zalogować się" ([prd.md:59](../../foundation/prd.md:59))
  and an e-mail + password account ([prd.md:99](../../foundation/prd.md:99)). The PRD has no hits for profile,
  phone, username, 2FA, passkeys, external login, password change or account deletion (searched in Polish and English).
- Roadmap: auth is baseline ([roadmap.md:71](../../foundation/roadmap.md:71)); e-mail sending is parked
  ([roadmap.md:193](../../foundation/roadmap.md:193)); diet is parked as "raczej cecha profilu użytkownika"
  ([roadmap.md:194](../../foundation/roadmap.md:194)), so no profile feature is planned.
- `ManageNavMenu` items ([ManageNavMenu.razor:7-30](../../../Components/Account/Shared/ManageNavMenu.razor:7)): Profile,
  Email, Password, External logins (hidden: no providers registered, `hasExternalLogins` is false), Two-factor
  authentication, Passkeys, Personal data. Of these, only Password relates to the PRD's account model;
  Personal data is arguably backed by the privacy non-goal ([prd.md:104](../../foundation/prd.md:104)).
- Phone number: read and written only in [Index.razor](../../../Components/Account/Pages/Manage/Index.razor) (lines 26-28, 58-60, 71-76, 87-89);
  `ApplicationUser` adds no fields. Save updates only the phone.
- Language: `<html lang="en">` ([App.razor:2](../../../Components/App.razor:2)); product pages and two main-nav items are Polish
  ("Moje produkty", "Przepisy"); the Manage view has about 18 English strings (headings, menu, labels,
  placeholders, button, status messages). The main nav itself is mixed (Home, Logout, Register, Login in English).

### 5. 7-state matrix — first read for this view

| State | Current state on `/Account/Manage` |
| --- | --- |
| default | Tokens throughout, except `hr` and the disabled label (C1). |
| hover | Nav pills and links use `--bs-link-hover-color`; Save uses `--primary-hover`. Token-driven. |
| focus-visible | Save and inputs use the `--ring` focus ring; **nav pills show the literal blue ring** (C1). `h1:focus { outline: none }` ([app.css:332](../../../wwwroot/app.css:332)) applies to the layout `h1` that `FocusOnNavigate` targets ([Routes.razor:9](../../../Components/Routes.razor:9)). |
| disabled | Username input: bg `--muted`, label literal `#6c757d` (C1). Save is never disabled. |
| error | Field: `ValidationMessage` under the input (destructive colour, text not colour alone). Server: `StatusMessage` danger, but chosen by string prefix (C3). |
| empty | User with no phone: field shows its placeholder right-aligned. No list or data region, so no empty-state component applies; candidate N/A. |
| loading | Static SSR full-page POST (`EditForm … method="post"`, [Index.razor:18](../../../Components/Account/Pages/Manage/Index.razor:18)); no in-page pending state, and no interactivity is allowed here ([CLAUDE.md](../../../CLAUDE.md) hard rule). Candidate N/A, or a CSS-only submitted state. |

## Code References

- `Components/Account/Pages/Manage/Index.razor:11-33` — page title, h3, StatusMessage, form with disabled Username, phone, Save
- `Components/Account/Shared/ManageLayout.razor:4-16` — h1/h2/hr stack and `col-lg-3`/`col-lg-9` grid
- `Components/Account/Shared/ManageNavMenu.razor:6-31` — `ul.nav.nav-pills.flex-column`, seven items (one conditional)
- `Components/Account/Shared/StatusMessage.razor:3` — variant chosen by `StartsWith("Error")`
- `Components/Layout/NavMenu.razor:34-35` — only entry link, labelled with the user name (= e-mail)
- `Components/App.razor:2` — `<html lang="en">`
- `wwwroot/app.css:212-243,297-300,320-326,362-369` — token mapping, nav-pills, focus ring, floating placeholder
- `wwwroot/lib/bootstrap/dist/css/bootstrap.css:209-215,2695-2698,3851-3854` — `hr`, disabled floating label, nav-link focus-visible
- `Components/Pages/Products.razor:16,21-25,66` — product page heading, field and button conventions
- `Components/Account/Pages/Manage/Email.razor:89,108` and `Components/Account/Pages/ConfirmEmailChange.razor:45` — misclassified status messages

## Architecture Insights

- Contract variant: **existing design system** (Bootstrap 5.3.3 + shadcn-named tokens in `app.css`), with
  **no component layer**. The extension path is the one `recipes-ui` used: re-point Bootstrap selectors
  compiled with literals in `app.css`, and change token values rather than component classes.
- The Account section has its own design language from the template (floating labels, `btn-lg w-100`,
  h1/h2/h3 stack) that diverges from the product pages (label-above, normal buttons, single h1). The
  Identity pages are the larger body of markup (26 floating fields vs 4 label-above fields), so choosing
  which style wins also decides how far the change reaches beyond the one view.
- `StatusMessage` couples presentation (variant) to message text. Any shared Alert introduced for C2/C3
  needs an explicit variant parameter, and the Identity cookie can carry the variant separately or by
  convention, which is a plan decision.
- `Components/Account/` must stay static SSR, so states needing runtime interactivity (spinner while
  saving, live validation) are out; only server-rendered or CSS-only states are available.

## Historical Context (from prior changes)

- `context/changes/recipes-ui/research.md` §5 lists shared-component candidates (StatusBadge, RecipeCard,
  ScoreSummary, Alert with action, LoadingState, EmptyState, **FormField**, SegmentedControl, Disclosure,
  nav icon). Its Open Question 2 (where shared components live, e.g. `Components/Ui/`) is still open;
  no `plan.md` exists for `recipes-ui`, though its theme commits (`2ab284a`, `f05bdfa`) are merged.
- `context/changes/recipes-ui/research.md` §4 recorded 0 colour literals in `Components/Account/**`; still
  true at `df44e65` (C1 literals come from compiled Bootstrap, not from the view).
- `context/archive/2026-10-03-pantry-add-products/plan.md` covers an anonymous `/products` request redirecting
  to `/Account/Login`; no Account UI decisions.
- `context/changes/deployment/deployment-plan.md` documents that Production runs with
  `RequireConfirmedAccount: false`; no Account UI decisions.
- `context/foundation/lessons.md` and `docs/reference/contract-surfaces.md` do not exist.

## Related Research

- [context/changes/recipes-ui/research.md](../recipes-ui/research.md) — token inventory, Bootstrap literal map, component candidates.

## Open Questions

Questions 1–4 are resolved in *Triage* (2026-10-06): translate this view, trim to PRD, label-above, `Components/Ui/`.
Questions 5–6 remain for the plan.

1. **Language (C5).** Translate the Manage view (and `lang`) to Polish in this change, or defer? If translated, C3 must be fixed in the same change, or errors render green.
2. **Content scope (C5).** Keep, hide or remove the phone field and the "Username" field; trim `ManageNavMenu` to PRD-relevant items (e.g. Password, Personal data) and hide Email change while no e-mail is sent? Hiding a nav item leaves its route reachable by URL.
3. **Field style (C2).** Which form-field style is the app's: floating labels (26 Account occurrences) or label-above (Products)? Extracting a `FormField` for this view without deciding leaves the drift in place.
4. **Component location.** Inherited from `recipes-ui` Open Question 2: e.g. `Components/Ui/` (namespace must not clash with a page class, per CLAUDE.md).
5. **Kitchen sink.** No kitchen-sink or styleguide route exists; the visual gate needs one (or a test-only route) that renders the 7 states. It must work under static SSR for Account components that read `HttpContext`.
6. **Out of view.** The logout `ReturnUrl` loop (§3) and the mixed-language main nav are outside this view; track separately or include as a ride-along in the plan.
