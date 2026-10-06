---
change_id: account-ui
title: Account ui
status: implemented
created: 2026-10-06
updated: 2026-10-06
archived_at: null
---

## Notes

<!-- Free-form notes for this change: links, ad-hoc context, decisions that don't belong in research/frame/plan. -->

- View: `/Account/Manage` (`Components/Account/Pages/Manage/Index.razor`, with `Components/Account/Shared/ManageLayout.razor`, `ManageNavMenu.razor`, `StatusMessage.razor`).
- Token source: `wwwroot/app.css` (shadcn-named tokens mapped onto Bootstrap 5.3 `--bs-*` variables).
- Contract variant: existing design system. Pre-audit hardcoded-value scan on the view: 0 hits.
- Constraint: `Components/Account/` pages stay static SSR (no `@rendermode`).
