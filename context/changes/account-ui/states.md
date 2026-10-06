# Account UI: 7-state matrix and deferred charges

Recorded at the Phase 4 gate (2026-10-06). The states are shown on the Development-only kitchen sink `/dev/ui`
(`Components/Pages/DevUi.razor`). The real view is `/Account/Manage`.

## 7-state matrix

| State | Result | Where it was checked |
| --- | --- | --- |
| default | Pass. All FormField variants and the menu are built from tokens. | `/dev/ui`, `/Account/Manage`, `/products` |
| hover | Pass. Pills and the button change on hover through token colours. | `/dev/ui` |
| focus-visible | Pass. Input, button and pills show the green `--ring` focus ring. | `/dev/ui`, `/Account/Manage` |
| disabled | Pass. The disabled input and button look disabled and stay readable. | `/dev/ui` |
| error | Pass. The field error sits under the field with the `.invalid` outline. The Error and legacy "Error:" StatusMessages render `alert-danger`; the Success one renders `alert-success`. | `/dev/ui` |
| empty | N/A. Profile has no list or data region, and a signed-in user always has an e-mail. | n/a |
| loading | N/A. The page is static SSR and renders complete on the server, with no in-page async region. Interactivity is forbidden under `Components/Account/`. | n/a |

Other checks:

- `/dev/ui` returns the not-found page when the app runs in Production (4.8).
- `/Account/Manage` and `/products` at desktop and 375 px show no regression (4.7).

Screenshots: the checks were done by hand in the browser by the change owner. No screenshot files were saved into
the repo, so there are no file names to list. Add them here if they are captured later.

## Deferred charges

Carried over from `research.md` *Triage* (reasons there):

- Floating labels elsewhere: the other 24 `form-floating` fields in Account pages move to `FormField` later.
- Translating the rest of Account: Login, Register, Password, Personal data and so on.
- Main-nav entry label (raw e-mail) and the mixed-language main nav.
- `ConfirmEmailChange.razor:45` (missing `$`, wrong variant). Now a one-line fix with `StatusKind`.
- Logout `ReturnUrl` loop.
- Migrating the other status redirects and `Message=` call sites to an explicit `StatusKind`.
- An `Info`/neutral `StatusKind`, to be added when `Email.razor` is migrated.
