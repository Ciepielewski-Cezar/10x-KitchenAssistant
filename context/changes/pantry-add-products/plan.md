# Private pantry list: adding products (S-01) Implementation Plan

## Overview

Give each signed-in user a private product list that exists without any setup, and let them add a product to one of the two PRD categories: „zużyj w pierwszej kolejności” or „w szafkach i zamrażalniku”. This is roadmap slice S-01 (FR-001, FR-002, US-01, Access Control). It adds the first domain entity, `Product`, which S-02 will hand to the AI by ID.

## Current State Analysis

- **No domain entities exist.** `Data/ApplicationDbContext.cs:7-11` holds only the Identity tables and `DataProtectionKeys`, and `Data/ApplicationUser.cs:6` is an empty `IdentityUser`.
- **DbContext is registered as scoped** with `AddDbContext` (`Program.cs:28-29`). That is fine for static SSR and Identity, but wrong for an interactive Blazor page, where one scope lives as long as the circuit.
- **Render mode is chosen per page.** `Components/App.razor:18` renders `<Routes />` without a global render mode, and `Components/Pages/Counter.razor:2` shows how a page opts into `InteractiveServer`.
- **Auth gating already works.** `Components/Routes.razor:4-8` uses `AuthorizeRouteView` with `RedirectToLogin`, and `Components/Pages/Auth.razor:5` shows the `[Authorize]` page pattern.
- **The nav menu** (`Components/Layout/NavMenu.razor:39-55`) has an `<AuthorizeView><Authorized>` block where a signed-in-only link fits.
- **No test project exists.** `KitchenAssistant.slnx` lists only `KitchenAssistant.csproj`. The planned CI (`context/changes/deployment/deployment-plan.md:197`) already runs `dotnet test`.
- **The app project sits at the repository root.** Its default `**/*.cs` glob would pull in any test project placed in a subfolder.

## Desired End State

A signed-in user opens „Moje produkty” (`/products`) from the nav menu or from Home. The page shows two sections, „Zużyj w pierwszej kolejności” first, then „W szafkach i zamrażalniku”. Each is sorted A→Z using Polish collation and shows a hint when empty.

The add form takes:
- a name (required, at most 100 characters after trimming)
- a category (required, no default)
- an optional free-text quantity (at most 50 characters)
- an optional expiry date
- an optional storage location: Lodówka / Spiżarnia / Zamrażarka

A product whose expiry date is today or earlier, by the date in Europe/Warsaw, carries a „Sprawdź termin” flag. A date of tomorrow or later is not flagged.

Adding a name that already exists in the **same category** for the same user is rejected. The comparison trims the name and ignores case, and storage location plays no part in it. The same name in the other category is accepted. Users never see each other's products, and anonymous users are redirected to login.

Verify with `dotnet test KitchenAssistant.slnx` (service rules) and the Phase 2 browser walkthrough, using two accounts.

### Key Discoveries:

- `Program.cs:28-29`: replace `AddDbContext` with `AddDbContextFactory`, which also registers a scoped `ApplicationDbContext`, so `AddEntityFrameworkStores` (`Program.cs:38`) keeps working.
- `Components/Account/IdentityRevalidatingAuthenticationStateProvider.cs:24-25`: the template already avoids long-lived scopes by creating a fresh scope per use. The factory follows the same idea.
- `Components/Layout/NavMenu.razor:34-36`: nav items use icon classes defined in `NavMenu.razor.css`. Reuse an existing class such as `bi-list-nested-nav-menu` rather than adding SVG.
- PRD *Business Logic*: S-02 sends products to the AI with their IDs, so `Product.Id` must be stable and compact (`int`).

## What We're NOT Doing

- Editing or deleting products (S-06, FR-003).
- Any recipe generation, scoring or AI call (S-02…S-05).
- Changing the category automatically based on expiry date. The category is always the user's choice.
- Structured quantity (number + unit) or any arithmetic on quantity.
- A separate „Pantry”/„Space” entity, sharing or invitations (PRD Non-Goals). The private list is simply the set of products where `UserId = me`.
- Translating the template Identity pages, nav items or Home text beyond the new link.
- Removing the template sample pages (Counter, Weather, Auth).
- Redirecting login or registration to `/products`.
- Making storage location part of the uniqueness rule.

## Implementation Approach

1. **Data and rules first.** Add the entity, migration and `ProductService`, and prove privacy, duplicates, sorting and the expiry flag with unit tests before any UI exists.
2. **Then a thin page.** `/products` is an `InteractiveServer` page that calls `ProductService`. That service is the only place that reads or writes products, and every method takes the `userId` explicitly.

Uniqueness is enforced by a unique index on `(UserId, Category, NormalizedName)`, where `NormalizedName` is the trimmed name passed through `ToUpperInvariant()` (the same idea Identity uses). This makes the rule independent of the database collation, so the same rule holds on SQL Server and on the SQLite used in tests.

## Critical Implementation Details

- **The test project lives under the app project's folder.** `KitchenAssistant.csproj` is at the repository root, so `KitchenAssistant.Tests/**` must be excluded from its default items, for example `<DefaultItemExcludes>$(DefaultItemExcludes);KitchenAssistant.Tests/**</DefaultItemExcludes>`. Otherwise the test sources compile into the app and the build breaks.
- **Use `AddDbContextFactory` instead of `AddDbContext`, not both.** Registering both produces an options-lifetime conflict at startup.
- **The interactive page must not inject `UserManager` or `ApplicationDbContext`.** Both would pin a DbContext to the circuit. Read the user ID from the cascaded `AuthenticationState`, using the `ClaimTypes.NameIdentifier` claim, and pass it to `ProductService`.
- **`OnModelCreating` must call `base.OnModelCreating` first,** or the Identity model breaks.
- **Use `ToUpperInvariant`, not `ToUpper`.** The culture-sensitive version would make the normalised name depend on the server's culture.

## Phase 1: Data model, service and tests

### Overview

Add the `Product` entity and migration, switch DbContext registration to the factory, and add a `ProductService` that holds every S-01 rule. Create `KitchenAssistant.Tests` to prove those rules.

### Changes Required:

#### 1. Domain entity and enums

**File**: `Data/Product.cs`, `Data/ProductCategory.cs`, `Data/StorageLocation.cs` (new)

**Intent**: The first per-user domain entity, keyed to `ApplicationUser` as CLAUDE.md requires.

**Contract**:
- `Product { int Id; string UserId; string Name; string NormalizedName; ProductCategory Category; string? Quantity; DateOnly? ExpiresOn; StorageLocation? StorageLocation }`
- `enum ProductCategory { UseFirst, Stored }`
- `enum StorageLocation { Fridge, Pantry, Freezer }`

#### 2. DbContext mapping

**File**: `Data/ApplicationDbContext.cs`

**Intent**: Expose `Products` and configure the schema.

**Contract**:
- `DbSet<Product> Products`
- Field limits: `Name` and `NormalizedName` are required, max 100. `Quantity` is max 50. Both enums are stored as strings, max 20. `ExpiresOn` is a `date`.
- Foreign key `UserId` → `AspNetUsers.Id`, required, `OnDelete(Cascade)`, with no navigation property on `ApplicationUser`.
- Unique index on `(UserId, Category, NormalizedName)`.

#### 3. Migration

**File**: `Data/Migrations/<timestamp>_AddProducts.cs` (generated)

**Intent**: Create the `Products` table. Generate it with `dotnet ef migrations add AddProducts --output-dir Data/Migrations`, then review the generated file. The unique index and the cascade foreign key must be present.

**Contract**: one new table and no changes to existing tables. The model snapshot is updated.

#### 4. Service registration

**File**: `Program.cs`

**Intent**: Make DbContext safe for interactive pages and give the service a clock it can be tested with.

**Contract**:
- `AddDbContext<ApplicationDbContext>(…)` becomes `AddDbContextFactory<ApplicationDbContext>(…)` with the same `UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure())` options.
- Add `AddSingleton(TimeProvider.System)`.
- Add `AddScoped<ProductService>()`.

#### 5. Product service and form model

**File**: `Products/ProductService.cs`, `Products/ProductForm.cs`, `Products/ProductLabels.cs` (new; namespace `KitchenAssistant.Products`)

**Intent**: The single place where products are read and written. Every query filters by `userId`, which is how privacy is enforced. The service applies normalisation and the duplicate rule, and computes the expiry flag from the date in Europe/Warsaw.

**Contract**:
- **Constructor:** `ProductService(IDbContextFactory<ApplicationDbContext>, TimeProvider)`. It creates one short-lived context per call.
- **Listing:** `Task<ProductList> GetProductsAsync(string userId, CancellationToken ct = default)` returns `ProductList(IReadOnlyList<ProductListItem> UseFirst, IReadOnlyList<ProductListItem> Stored)`.
  - Each section is sorted by `Name` in C# with `StringComparer.Create(new CultureInfo("pl-PL"), ignoreCase: true)`.
  - `ProductListItem(int Id, string Name, ProductCategory Category, string? Quantity, DateOnly? ExpiresOn, StorageLocation? StorageLocation, bool IsExpiryDue)`.
- **Adding:** `Task<AddProductResult> AddProductAsync(string userId, ProductForm form, CancellationToken ct = default)` returns `enum AddProductResult { Added, Duplicate }`.
  - Trims the name and sets `NormalizedName = Name.ToUpperInvariant()`.
  - Trims the quantity and turns an empty value into `null`.
  - Checks for an existing `(userId, Category, NormalizedName)` before saving and returns `Duplicate` if one is found.
  - On `DbUpdateException`, re-checks for the duplicate and returns `Duplicate`, or rethrows if there is none. This covers two adds racing.
  - Throws `ArgumentException` for an empty or over-long name, a missing category, or an over-long quantity. These are programming errors, because the form validates first.
- **Expiry flag:** `static bool IsExpiryDue(DateOnly? expiresOn, DateOnly today)` returns `expiresOn is not null && expiresOn <= today`. "Today" is `TimeProvider.GetUtcNow()` converted to `TimeZoneInfo.FindSystemTimeZoneById("Europe/Warsaw")`.
- **Form model:** `ProductForm` uses DataAnnotations with full Polish `ErrorMessage` strings (no `{0}` placeholder):
  - `Name`: `[Required]`, `[StringLength(100)]`
  - `Category`: `ProductCategory?`, `[Required]`
  - `Quantity`: `string?`, `[StringLength(50)]`
  - `ExpiresOn`: `DateOnly?`
  - `StorageLocation`: `StorageLocation?`
- **Labels:** `ProductLabels` maps the enums to Polish text: „Zużyj w pierwszej kolejności”, „W szafkach i zamrażalniku”, „Lodówka”, „Spiżarnia”, „Zamrażarka”.

#### 6. Test project

**File**: `KitchenAssistant.Tests/KitchenAssistant.Tests.csproj`, `KitchenAssistant.Tests/ProductServiceTests.cs` (new), `KitchenAssistant.slnx`, `KitchenAssistant.csproj`

**Intent**: The first test project. It proves the S-01 rules against a real relational provider, without needing LocalDB, so the tests also run on the Linux CI runner.

**Contract**:
- **Project:** an xUnit project (`dotnet new xunit`, `net10.0`) that references `../KitchenAssistant.csproj` and adds:
  - `Microsoft.EntityFrameworkCore.Sqlite` 10.0.12
  - `Microsoft.Extensions.TimeProvider.Testing`
- **Registration:** the project is registered in `KitchenAssistant.slnx`, and `KitchenAssistant.csproj` excludes `KitchenAssistant.Tests/**` (see Critical Implementation Details).
- **Test database:** the tests use an open in-memory SQLite connection with `EnsureCreated`, a small `IDbContextFactory` over that connection, and seeded `ApplicationUser` rows for user A and user B, because foreign keys are enforced.

### Success Criteria:

#### Automated Verification:

- Solution builds: `dotnet build KitchenAssistant.slnx`
- Service tests pass: `dotnet test KitchenAssistant.slnx` (all cases listed under Testing Strategy → Unit Tests)
- No pending model changes: `dotnet ef migrations has-pending-model-changes` exits 0
- Migration applies to LocalDB: `dotnet ef database update`
- Generated `AddProducts` migration creates `Products` with a cascade FK to `AspNetUsers` and a unique index on `(UserId, Category, NormalizedName)`

#### Manual Verification:

- Identity still works after the DbContext factory switch: with `dotnet run --launch-profile https`, register, confirm via the on-screen link, log in and log out

**Implementation Note**: After completing this phase and all automated verification passes, pause here for manual confirmation from the human that the manual testing was successful before proceeding to the next phase.

---

## Phase 2: `/products` page and navigation

### Overview

The Polish, interactive „Moje produkty” page with the add form and the two category sections, plus links from the nav menu and Home.

### Changes Required:

#### 1. Products page

**File**: `Components/Pages/Products.razor` (new)

**Intent**: The user's private list and the add form, interactive so that several products can be added in a row without page reloads.

**Contract**:
- **Page setup:** `@page "/products"`, `@rendermode InteractiveServer`, `@attribute [Authorize]`, `<PageTitle>Moje produkty</PageTitle>`, `<h1>Moje produkty</h1>`. Injects `ProductService`. Gets the `userId` from `[CascadingParameter] Task<AuthenticationState>`.
- **Form:**
  - An `EditForm` over a `ProductForm` with `DataAnnotationsValidator`.
  - Inputs: a text input for the name; a radio group for the two categories with nothing preselected; a text input for quantity; `InputDate<DateOnly?>` for the expiry date; `InputSelect<StorageLocation?>` with an empty „—” option.
  - `InputDate` and `InputSelect` get a Polish `ParsingErrorMessage` (e.g. „Podaj poprawną datę.”). No validation message uses the `{0}` placeholder, so C# property names never show.
  - The submit button reads „Dodaj” and is disabled while saving.
- **After a successful add:** reload the list, reset the form but keep the chosen `Category`, and move focus back to the name input.
- **On `Duplicate`:** show „Produkt „{name}” jest już w kategorii „{category label}”.” and keep the input.
- **On an unexpected exception** while loading the list or adding a product: catch it in the page, log it through an injected `ILogger<Products>`, show „Nie udało się zapisać produktu. Spróbuj ponownie.” (or „Nie udało się wczytać produktów.” when loading), and keep the input. An exception must not reach the circuit.
- **List:**
  - Two sections, `<h2>` „Zużyj w pierwszej kolejności” then „W szafkach i zamrażalniku”, in the order returned by the service.
  - Each row shows the name, plus the quantity, location label and expiry date (`dd.MM.yyyy`) when present.
  - A row with `IsExpiryDue` gets a warning style and a „Sprawdź termin” badge.
  - An empty section shows „Brak produktów w tej kategorii.”

#### 2. Navigation

**File**: `Components/Layout/NavMenu.razor`

**Intent**: Make the list reachable for signed-in users.

**Contract**: inside `<AuthorizeView><Authorized>`, before the account item, add `NavLink href="products"` labelled „Moje produkty”, reusing an existing nav icon class.

#### 3. Home link

**File**: `Components/Pages/Home.razor`

**Intent**: A signed-in user landing on Home gets a direct path to the list.

**Contract**: an `<AuthorizeView><Authorized>` block with a link to `products` labelled „Przejdź do swoich produktów”. The rest of Home is unchanged, and the page stays static SSR.

### Success Criteria:

#### Automated Verification:

- Solution builds: `dotnet build KitchenAssistant.slnx`
- Tests still pass: `dotnet test KitchenAssistant.slnx`
- Anonymous request is redirected to login: with the app running, `curl -sk -o /dev/null -w "%{http_code} %{redirect_url}" https://localhost:7020/products` prints `302` and a URL containing `/Account/Login`

#### Manual Verification:

- Signed in on a fresh account: „Moje produkty” appears in the nav menu, Home shows the link, and `/products` shows both sections with the empty hint
- Adding a product with only a name and category puts it in the right section in A→Z order, clears the form and keeps the chosen category
- A product with quantity, expiry date and location shows all three; today's and past dates show „Sprawdź termin”, tomorrow's does not
- Adding „ Mleko” when „mleko” is in the same category is rejected with the duplicate message; the same name in the other category is accepted
- An empty name, no category, or a name over 100 characters shows Polish validation messages and saves nothing
- A second account sees an empty list and none of the first account's products; reloading the page keeps all data
- Production-like stack: `./scripts/local-prod.ps1` applies the migration at startup, and the same add flow works at http://localhost:8090/products

**Implementation Note**: After completing this phase and all automated verification passes, pause here for manual confirmation from the human that the manual testing was successful before proceeding to the next phase.

---

## Testing Strategy

### Unit Tests:

`ProductServiceTests`, run on SQLite in memory:
- **Add and list:** an added product appears in the correct section with every field intact.
- **Normalisation:** the name `"  jajka "` is stored as `"jajka"`, and a whitespace-only quantity is stored as `null`.
- **Duplicates:** with `"mleko"` in `UseFirst`, adding `" Mleko "` to `UseFirst` returns `Duplicate` and the count stays the same. Adding `"mleko"` to `Stored` returns `Added`.
- **Privacy:** user B's list does not contain user A's products, and user B can add `"mleko"` to `UseFirst` even though A has it.
- **Polish sorting:** `["masło", "łosoś", "lody"]` lists as `["lody", "łosoś", "masło"]`.
- **Expiry flag:** with `FakeTimeProvider` at `2026-10-03T22:30Z`, which is already `2026-10-04 00:30` in Warsaw:
  - expires `2026-10-04` → `IsExpiryDue = true` (today in Warsaw)
  - expires `2026-10-03` → `true`
  - expires `2026-10-05` → `false`
  - no date → `false`

### Integration Tests:

- None automated in this change. The page is covered by the Phase 2 manual walkthrough.

### Manual Testing Steps:

1. Log in as `a@kitchen.test`, open „Moje produkty”, and add „mleko” (zużyj w pierwszej kolejności, „1 l”, today's date, Lodówka). It should show „Sprawdź termin”.
2. Add „ Mleko” to the same category. The duplicate message should appear. Add it to „W szafkach i zamrażalniku” instead. It should be accepted.
3. Add „masło”, „łosoś” and „lody” to one category. They should be listed as lody, łosoś, masło.
4. Log in as `b@kitchen.test`. The list should be empty.
5. Log out and open `/products`. You should be redirected to login.

## Performance Considerations

The list is loaded in full per user. It holds tens of products, so in-memory sorting is fine. Because the page is prerendered, it loads twice (once on prerender, once when interactive), which is acceptable at this size.

## Migration Notes

`AddProducts` is purely additive. Production applies it at startup through `Database:MigrateOnStartup` (`Program.cs:67-71`), and `local-prod.ps1 -Reset` rebuilds the schema from migrations. Deleting an account (Identity „Delete personal data”) also deletes that user's products through the cascade foreign key.

## References

- Roadmap slice: `context/foundation/roadmap.md` (S-01)
- PRD: `context/foundation/prd.md` (FR-001, FR-002, US-01, Access Control, Business Logic on product IDs)
- Interactive page pattern: `Components/Pages/Counter.razor:2`
- Authorize page pattern: `Components/Pages/Auth.razor:5`
- DbContext registration: `Program.cs:28-29`

## Progress

> Convention: `- [ ]` pending, `- [x]` done. Append ` — <commit sha>` when a step lands. Do not rename step titles. See `references/progress-format.md`.

### Phase 1: Data model, service and tests

#### Automated

- [x] 1.1 Solution builds: `dotnet build KitchenAssistant.slnx`
- [x] 1.2 Service tests pass: `dotnet test KitchenAssistant.slnx`
- [x] 1.3 No pending model changes: `dotnet ef migrations has-pending-model-changes` exits 0
- [x] 1.4 Migration applies to LocalDB: `dotnet ef database update`
- [x] 1.5 Generated `AddProducts` migration creates `Products` with cascade FK and unique index `(UserId, Category, NormalizedName)`

#### Manual

- [x] 1.6 Identity still works after the DbContext factory switch (register, confirm, log in, log out)

### Phase 2: `/products` page and navigation

#### Automated

- [ ] 2.1 Solution builds: `dotnet build KitchenAssistant.slnx`
- [ ] 2.2 Tests still pass: `dotnet test KitchenAssistant.slnx`
- [ ] 2.3 Anonymous request to `/products` is redirected (302) to `/Account/Login`

#### Manual

- [ ] 2.4 Fresh account sees nav link, Home link and both sections with empty hints
- [ ] 2.5 Name + category add lands in the right section A→Z, form clears and keeps category
- [ ] 2.6 Quantity, expiry date and location are shown; today/past dates flagged „Sprawdź termin”, tomorrow not
- [ ] 2.7 Same-category duplicate rejected with message; other category accepted
- [ ] 2.8 Invalid input shows Polish validation messages and saves nothing
- [ ] 2.9 Second account sees only its own products; data survives reload
- [ ] 2.10 Production-like stack applies the migration at startup and the add flow works at http://localhost:8090/products
