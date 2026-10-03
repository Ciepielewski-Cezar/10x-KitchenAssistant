# Private pantry list: adding products (S-01) — Plan Brief

> Full plan: `context/changes/pantry-add-products/plan.md`

## What & Why

A signed-in user gets a private product list that exists without any setup, and can add products to the two PRD categories: „zużyj w pierwszej kolejności” and „w szafkach i zamrażalniku”. This is roadmap slice S-01 (FR-001, FR-002, US-01, Access Control). It provides the input that recipe generation (S-02) needs, with each product carrying a stable ID for the AI.

## Starting Point

The app is the `dotnet new blazor --auth Individual` template, with Identity, cookie auth and SQL Server migrations working. There are no domain entities, no product pages and no test project. DbContext is registered as scoped, which is unsafe for interactive Blazor pages.

## Desired End State

„Moje produkty” (`/products`) is reachable from the nav menu and from Home when signed in. It shows two sections, „Zużyj w pierwszej kolejności” first, each sorted A→Z with Polish collation. A product has a name, a category, and optionally a free-text quantity, an expiry date and a storage location (Lodówka / Spiżarnia / Zamrażarka). Expiry dates that are today or earlier (by the date in Europe/Warsaw) are flagged „Sprawdź termin”. A duplicate name in the same category is rejected, but the same name is allowed in the other category. Each user sees only their own products.

## Key Decisions Made

| Decision | Choice | Why (1 sentence) |
| --- | --- | --- |
| Product fields | Name, category, plus optional quantity, expiry date and storage location | The user wants richer data now; only name and category are required, so adding stays fast. |
| Quantity | Optional free text, at most 50 characters | Covers „500 g”, „2 szt.”, „pół słoika”; nothing in the MVP calculates with it. |
| Expiry date | Optional; flagged when it is today or earlier; never changes the category | Useful at a glance; the category stays the user's choice as the PRD intends. |
| Storage location | Optional: Lodówka, Spiżarnia or Zamrażarka; not part of uniqueness | An extra descriptive field requested by the user. |
| Duplicates | Trimmed, case-insensitive name unique per user **and category** | Allows „mleko” both open in the fridge and sealed in the cupboard. |
| Placement | New `[Authorize]` page `/products`, plus a nav link and a Home link | Gives S-02 a clean route to sit next to; Identity pages are untouched. |
| List order | Two sections, „use first” first, each A→Z (pl-PL); a hint when a section is empty | Mirrors the FR-002 categories and makes a known product easy to find. |
| UI language | Polish on new UI only | Matches the users and the PRD wording; template pages are left alone. |
| Render mode and data access | `InteractiveServer` page, with `AddDbContextFactory` replacing `AddDbContext` | Gives a short-lived DbContext per operation; S-02 and S-06 will need the same pattern. |
| Privacy and uniqueness | `ProductService` filters every call by `userId`; a unique index on `(UserId, Category, NormalizedName)` | One place enforces privacy, and the database guarantees the duplicate rule. |
| Tests | New xUnit `KitchenAssistant.Tests`, running against in-memory SQLite and a fake clock | Real relational checks without LocalDB, so they also run on the Linux CI runner. |

## Scope

**In scope:**
- `Product` entity and the `AddProducts` migration
- `ProductService`: list, add, duplicate rule, expiry flag
- DbContext factory and `TimeProvider` registration
- The `/products` page, the nav link and the Home link
- The test project with service tests

**Out of scope:**
- Editing or deleting products (S-06)
- Recipes and AI (S-02…S-05)
- Changing the category from the expiry date
- Structured quantity
- Sharing or a separate pantry entity
- Translating template pages
- Redirecting login to `/products`

## Architecture / Approach

`Products.razor` (`InteractiveServer`, `[Authorize]`) reads the user ID from the cascaded `AuthenticationState` and calls `ProductService`. The page never injects `UserManager` or DbContext. `ProductService` creates one short-lived `ApplicationDbContext` per call through `IDbContextFactory`, normalises the input (trimming, and `NormalizedName = ToUpperInvariant`), checks for duplicates, groups products and sorts them with the pl-PL comparer, and computes `IsExpiryDue` from `TimeProvider` in Europe/Warsaw. `Product` has a required, cascading foreign key to `AspNetUsers`.

## Phases at a Glance

| Phase | What it delivers | Key risk |
| --- | --- | --- |
| 1. Data model, service and tests | Entity, migration, factory registration, `ProductService`, xUnit project | The factory switch could break Identity; the test project nested under the root project needs `DefaultItemExcludes` |
| 2. `/products` page and navigation | Polish interactive page, form, two sections, expiry flag, nav and Home links | DbContext or `UserManager` accidentally injected into the interactive page |

**Prerequisites:** LocalDB available and `dotnet tool restore` run. Docker is needed for the production-like stack check.
**Estimated effort:** about 2 sessions across 2 phases.

## Open Risks & Assumptions

- Assumes `AddDbContextFactory` registers a scoped `ApplicationDbContext` that Identity's stores resolve. The Phase 1 manual gate checks this.
- pl-PL sorting and the `Europe/Warsaw` time zone need ICU and tzdata. Both are present on Windows and in the standard `aspnet` container image. The Phase 2 production-like stack check confirms this.

## Success Criteria (Summary)

- A signed-in user adds products to both categories and sees them sorted, with expiry flags, on a page only they can see.
- Duplicate-in-category and validation errors show clear Polish messages and save nothing.
- `dotnet test` proves privacy, duplicates, sorting and the time-zone-aware expiry flag.
