<!-- IMPL-REVIEW-REPORT -->
# Implementation Review: Account UI (`/Account/Manage` Profile)

- **Plan**: context/changes/account-ui/plan.md
- **Scope**: Full plan (Phases 1-4 of 4)
- **Reviewed phases**: 1, 2, 3, 4
- **Date**: 2026-10-06
- **Verdict**: APPROVED
- **Findings**: 0 critical, 1 warning, 5 observations

## Verdicts

| Dimension | Verdict |
|-----------|---------|
| Plan Adherence | PASS |
| Scope Discipline | PASS |
| Safety & Quality | PASS |
| Architecture | PASS |
| Pattern Consistency | PASS |
| Success Criteria | WARNING |

Automated criteria re-run at review time: build 0 errors / 0 warnings, 122 of 122 tests pass, `app.css` literal count 104 (unchanged), literal scan on the touched `.razor` files 0 hits, `StartsWith("Error")` absent from `StatusMessage.razor`.

## Findings

### F1 — Phase 4 visual gate has no durable artifact

- **Severity**: ⚠️ WARNING
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Success Criteria
- **Location**: context/changes/account-ui/states.md:25-26, plan.md Progress 4.5 / 4.7 / 4.9
- **Detail**: Plan 4.9 requires the 7-state matrix, deferred charges and the screenshot file names in the change folder. The manual checks were done by hand and `states.md` says no screenshot files were saved. Rows 4.5, 4.7 and 4.9 are ticked, so the visual gate rests on the owner's confirmation alone.
- **Fix**: Capture `/dev/ui`, `/Account/Manage` and `/products` at desktop and 375 px, and list the file names in `states.md`. Or amend 4.9 to say names are not required.
- **Decision**: PARTIALLY FIXED. Saved `screenshots/dev-ui-desktop.png` and listed it in states.md. The 375 px, `/Account/Manage`, `/products`, hover and focus captures remain hand-checked only (headless Edge cannot go below ~500 px, the app blocks framing, the other two pages need a login). 4.9 stays ticked on that basis; the gap is written down.

### F2 — `<html lang="pl">` is global while most Account pages are English

- **Severity**: 👁 OBSERVATION
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Safety & Quality (accessibility)
- **Location**: Components/App.razor:2
- **Detail**: Screen readers pronounce the English Login/Register/ChangePassword/etc. content with Polish rules (WCAG 3.1.2). The plan accepted this as temporary.
- **Fix**: Keep until the other Account pages are translated, or add `lang="en"` to the legacy pages.
- **Decision**: SKIPPED. Accepted in the plan as temporary until the other Account pages are translated.

### F3 — Other Manage pages now jump from h1 to h3

- **Severity**: 👁 OBSERVATION
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Pattern Consistency
- **Location**: Components/Account/Pages/Manage/Index.razor:11
- **Detail**: Profile uses `<h2>`. The other Manage pages still use `<h3>` and the layout's `<h2>` was removed, so they skip a heading level.
- **Fix**: Move the other Manage pages to `<h2>` when they are migrated.
- **Decision**: FIXED. The 12 other Manage pages now use `<h2>` for their page heading (build passes, 0 warnings). Not committed yet.

### F4 — Email, 2FA and Passkeys are reachable only by URL

- **Severity**: 👁 OBSERVATION
- **Impact**: 🔎 MEDIUM — real tradeoff; pause to reason through it
- **Dimension**: Scope Discipline
- **Location**: Components/Account/Shared/ManageNavMenu.razor
- **Detail**: Intentional per the plan (PRD trim). Users have no UI path to change e-mail, set up 2FA or manage passkeys, and Profile shows the e-mail with no pointer to the Email page.
- **Fix**: Track as a product follow-up so the hiding does not become permanent.
  - Strength: Keeps this change within the PRD trim the owner chose.
  - Tradeoff: The routes stay as untested dead ends until someone decides.
  - Confidence: HIGH — plan lines 74 and 300 state this on purpose.
  - Blind spot: Whether the PRD ever needs e-mail change or 2FA.
- **Decision**: SKIPPED. Intentional PRD trim; routes stay reachable by URL.

### F5 — `FormField` leaves label/validation wiring to callers

- **Severity**: 👁 OBSERVATION
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Architecture
- **Location**: Components/Ui/FormField.razor:6-12
- **Detail**: `Id` must be repeated on the child input, the `ValidationMessage` is not tied to the input with `aria-describedby`, and callers without `For` pass a dummy `TValue="string"`. None of this is a regression from the old inline markup.
- **Fix**: In a later pass, give the validation message an id for `aria-describedby`, or document the `Id` contract.
- **Decision**: SKIPPED. Not a regression; revisit in a later FormField pass.

### F6 — New `…WithStatus` overloads have no production caller or test

- **Severity**: 👁 OBSERVATION
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Plan Adherence
- **Location**: Components/Account/IdentityRedirectManager.cs:46-60
- **Detail**: The Profile rewrite removed the only `RedirectToCurrentPageWithStatus` use, so the new overloads are unused. CLAUDE.md names them as the contract for new callers, so keeping them is correct.
- **Fix**: Keep them. Optionally add a test that the cookie written by the redirect manager decodes to the same kind.
- **Decision**: SKIPPED. Overloads kept as the documented contract; the cookie helper is already tested.
