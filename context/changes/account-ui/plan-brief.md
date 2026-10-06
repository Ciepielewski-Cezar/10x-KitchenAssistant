# Account UI (`/Account/Manage` Profile) — Plan Brief

> Full plan: `context/changes/account-ui/plan.md`
> Research: `context/changes/account-ui/research.md`

## What & Why

The Profile page is still the `dotnet new blazor --auth Individual` template in the app's colours:

- English copy in a Polish app
- three stacked headings
- a phone field nothing uses
- a status alert whose colour depends on the message starting with "Error", which would turn every Polish error green

This change puts the view on the design-system contract (tokens plus repo components) and trims it to the PRD's e-mail and password account.

## Starting Point

`app.css` already maps the shadcn tokens onto Bootstrap, but three Bootstrap literals still reach the view:
- the nav-link focus ring
- the disabled floating label
- `hr`

There is no shared component folder. Products builds label-above fields inline; Account uses floating labels. `StatusMessage` is shared by 24 Account pages and infers its variant from the text.

## Desired End State

A signed-in user opening their account sees a Polish page:
- the h1 "Moje konto"
- a labelled menu with Profil, Hasło and Dane osobowe (a row of pills on a phone, a column on desktop)
- the h2 "Profil" with one read-only E-mail field

Keyboard focus is the green `--ring` everywhere. Products uses the same `FormField`. Status alerts take their colour from an explicit `StatusKind`. A Development-only `/dev/ui` page shows every state.

## Key Decisions Made

| Decision | Choice | Why (1 sentence) | Source |
| --- | --- | --- | --- |
| Scope | C1–C5 in; Profile, layout, menu translated; `lang="pl"` | Triage of the five charges against the PRD | Research |
| Field style | Label-above `FormField` in `Components/Ui/` | Matches the product pages; Account's floating labels become legacy | Research |
| Menu content | Profil, Hasło, Dane osobowe (+ hidden-unless-configured external logins); routes kept | Only these relate to the PRD's account model; e-mail change sends nothing | Research |
| C3 mechanism | Explicit `StatusKind` in parameter and cookie; untagged callers keep `StartsWith("Error")` | Polish messages are safe now, without touching the 23 other pages | Plan |
| Profile content | Read-only E-mail via `FormField`; no form, no Save; Products' 4 fields adopt `FormField` | The view shows the field style, and the component gets two real callers | Plan |
| Headings | Layout h1 "Moje konto", page h2 "Profil" | One h1 per page; untranslated Manage pages keep their h3 under it | Plan |
| Mobile menu | `<nav aria-label="Ustawienia konta">`, pills horizontal below `lg` | Three short labels fit one row at 375 px | Plan |
| Visual gate | Static SSR `/dev/ui`, 404 outside Development | Shows states trimmed Profile can't reach (status error, field error, disabled) | Plan |
| Tests | xUnit on a plain `StatusMessageCookie` helper; no bUnit | Covers the only logic in the change with no new dependency | Plan |

## Scope

**In scope:**
- Three token re-points in `app.css`
- `Components/Ui/FormField` used on Profile and Products
- `StatusKind` + cookie helper + unit tests
- Profile rewritten in Polish without phone or Username
- One-h1 layout, labelled three-item menu, `lang="pl"`
- `/dev/ui` kitchen sink
- CLAUDE.md guard rule

**Out of scope:**
- The other Account pages (copy, floating labels, status call sites)
- `ConfirmEmailChange` bug
- Removing routes or the `PhoneNumber` column
- Main-nav label, language and logout loop
- Dark mode, an Info status kind, bUnit or Playwright

## Architecture / Approach

Contract before pixels. Phase 1 changes token mappings only. Phase 2 adds the two shared pieces and proves them on an existing caller (Products) and in unit tests. Phase 3 rebuilds the one view on top of them. Phase 4 renders the full 7-state matrix in a dev-only static page and writes the rule that keeps the next agent on `Components/Ui/` and `StatusKind`. `Components/Account/` stays static SSR throughout.

## Phases at a Glance

| Phase | What it delivers | Key risk |
| --- | --- | --- |
| 1. Token re-points (C1) | Green focus ring on menu pills; tokenised disabled label and `hr` | Selector specificity loses to Bootstrap |
| 2. Shared contract (C2, C3) | `FormField` on Products; `StatusKind` + helper + tests | Products' validation/focus behaviour regresses |
| 3. The Profile view (C4, C5) | Polish, trimmed Profile with one h1 and a labelled menu | Untranslated Manage pages look odd under the new layout |
| 4. States, gate, guard | `/dev/ui` matrix, screenshots, CLAUDE.md rule | Dev-only gating leaks to Production |

**Prerequisites:** LocalDB with a dev test account; `dotnet tool restore` done; no other in-flight change on `Components/Account/` or `Products.razor`.
**Estimated effort:** ~2 sessions across 4 phases (Phases 1 and 4 are small; Phase 2 carries the logic and the tests).

## Open Risks & Assumptions

- `lang="pl"` is global, so Login/Register are declared Polish while still English until their own change (accepted in triage).
- The menu is Polish but Hasło and Dane osobowe open English pages, until those pages are translated.
- Assumes .NET 10 `NavigationManager.NotFound()` re-executes `/not-found` under static SSR, as the existing status-code pipeline suggests. The Phase 4 Production check verifies it.
- Status cookies written by an older build decode as legacy values, so a deploy mid-redirect is harmless.

## Success Criteria (Summary)

- `/Account/Manage` is a short Polish page with one h1, a named three-item menu and a read-only e-mail, at desktop and 375 px, with no literals in the view files.
- A Polish error sent with `StatusKind.Error` renders red, and the untouched English pages still render their errors red.
- `/dev/ui` shows default, hover, focus-visible, disabled and error. Empty and loading are N/A with reasons. CLAUDE.md points the next agent at `Components/Ui/`, `FormField` and `StatusKind`.
