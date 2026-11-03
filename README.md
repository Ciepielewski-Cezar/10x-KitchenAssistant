# Kitchen Assistant

Aplikacja webowa, która na podstawie produktów, które użytkownik już ma w lodówce i w szafkach, generuje przy pomocy AI dopasowane przepisy. Zamiast prowadzić od przepisu do listy zakupów, prowadzi od posiadanych składników do przepisu.

## Funkcje

- Konto i logowanie (ASP.NET Core Identity). Dane każdego użytkownika są prywatne.
- Lista produktów w dwóch kategoriach: „zużyj w pierwszej kolejności” oraz „w szafkach i zamrażalniku” (dodawanie, edycja, usuwanie), strona `/products`.
- Wybór parametrów posiłku i generowanie przepisów przez AI (Claude, structured output), strona `/recipes`. W wynikach są też przepisy z krótką listą brakujących składników.
- Szczegóły przepisu: składniki i kroki przygotowania.
- Ocena przepisu, liczba brakujących składników i kolejność sortowania są liczone w kodzie aplikacji, a nie przez model.

Pełna specyfikacja produktu: [context/foundation/prd.md](context/foundation/prd.md). Uzasadnienie wyboru stacku: [context/foundation/tech-stack.md](context/foundation/tech-stack.md).

## Stack

| Obszar | Technologia |
| --- | --- |
| Aplikacja | .NET 10, Blazor Web App (statyczny SSR + Interactive Server per strona) |
| Autoryzacja | ASP.NET Core Identity, cookies |
| Baza danych | EF Core 10 + SQL Server (lokalnie LocalDB lub kontener SQL Server 2022, produkcyjnie Azure SQL) |
| AI | Anthropic C# SDK, model `claude-sonnet-5-5` |
| UI | Bootstrap 5.3 (skompilowany CSS, bez Node), tokeny w `wwwroot/app.css` |
| Testy | xUnit + SQLite in-memory (`KitchenAssistant.Tests`), Playwright E2E (`tests/`) |
| CI/CD | GitHub Actions, Azure App Service |

## Struktura repozytorium

```
Components/          strony i komponenty Razor (Pages/, Account/, Layout/, Ui/)
Data/                ApplicationDbContext, encje, migracje EF Core
Pantry/              logika produktów użytkownika (ProductService)
Recipes/             generowanie przepisów: prompt, schemat, klient Anthropic, ranking
KitchenAssistant.Tests/  testy jednostkowe i integracyjne
tests/               testy Playwright (e2e, smoke)
scripts/             local-prod.ps1 (lokalny stos produkcyjny) i skrypty CI
context/             PRD, plany zmian i dokumentacja procesu
```

## Uruchomienie po pobraniu z GitHuba

Są dwa sposoby. **Tryb deweloperski** jest najszybszy i nie wymaga klucza API (przepisy zwraca generator testowy). **Stos produkcyjny w Dockerze** uruchamia aplikację tak jak na serwerze, z prawdziwym generowaniem przepisów przez AI.

### Wymagania

- [Git](https://git-scm.com/)
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) (wersja wskazana w `global.json`: 10.0.100 lub nowsza z linii 10.0)
- Tryb deweloperski: **SQL Server LocalDB** (Windows; instaluje się z Visual Studio albo z SQL Server Express). Na macOS/Linux zobacz [Bez LocalDB](#bez-localdb-macos-linux).
- Stos produkcyjny: **Docker Desktop** (z Docker Compose) i PowerShell (na Windows wbudowany, na macOS/Linux `pwsh`).
- Klucz API Anthropic (`ANTHROPIC_API_KEY`), potrzebny tylko do prawdziwego generowania przepisów.

### 1. Sklonuj repozytorium

```bash
git clone https://github.com/Ciepielewski-Cezar/10x-KitchenAssistant.git
```

```bash
cd 10x-KitchenAssistant
```

### 2a. Tryb deweloperski (LocalDB)

Przywróć narzędzia lokalne (m.in. `dotnet-ef`):

```bash
dotnet tool restore
```

Zaufaj certyfikatowi deweloperskiemu HTTPS (jednorazowo na danym komputerze):

```bash
dotnet dev-certs https --trust
```

Utwórz bazę danych w LocalDB i zastosuj migracje:

```bash
dotnet ef database update
```

Uruchom aplikację:

```bash
dotnet run --launch-profile https
```

Aplikacja działa pod adresem https://localhost:7020 (oraz http://localhost:5180). Do pracy z hot reloadem użyj `dotnet watch`.

Pierwsze logowanie:

1. Kliknij **Register** i załóż konto.
2. W trybie deweloperskim e-mail nie jest wysyłany: link potwierdzający konto („Click here to confirm your account”) pojawia się na ekranie. Kliknij go, a potem zaloguj się.
3. Dodaj produkty w zakładce **Moje produkty**, a następnie wygeneruj przepisy w zakładce **Przepisy**.

**Generowanie przepisów w trybie deweloperskim.** Domyślnie (`appsettings.Development.json`) działa generator `Fake`, który po chwili zwraca przykładowe przepisy zbudowane z Twoich produktów, bez wywołania AI i bez kosztów. Ignoruje wybrane parametry posiłku. Aby używać prawdziwego modelu, zapisz klucz i przełącz generator w user-secrets (nie trafiają do repozytorium):

```bash
dotnet user-secrets set ANTHROPIC_API_KEY <twój-klucz>
```

```bash
dotnet user-secrets set Recipes:Generator Anthropic
```

#### Bez LocalDB (macOS, Linux)

LocalDB działa tylko na Windows. Na innych systemach uruchom sam kontener SQL Server ze stosu produkcyjnego i wskaż go w user-secrets:

1. Skopiuj `.env.example` do `.env` i ustaw `MSSQL_SA_PASSWORD` (min. 8 znaków: wielka i mała litera, cyfra, symbol).
2. Uruchom bazę: `docker compose up -d --wait sql` (port 14330 na hoście).
3. Ustaw connection string (podstaw swoje hasło):
   ```bash
   dotnet user-secrets set ConnectionStrings:DefaultConnection "Server=localhost,14330;Database=KitchenAssistant;User ID=sa;Password=<hasło>;Encrypt=True;TrustServerCertificate=True"
   ```
4. Dalej tak jak wyżej: `dotnet ef database update`, `dotnet run --launch-profile https`.

### 2b. Stos produkcyjny w Dockerze

Aplikacja działa w kontenerze Linux w środowisku `Production`, razem z kontenerem SQL Server 2022. Obraz buduje .NET SDK (`/t:PublishContainer`), bez Dockerfile. Migracje uruchamiają się przy starcie, a potwierdzanie konta e-mailem jest wyłączone.

1. Upewnij się, że Docker Desktop działa.
2. Skopiuj `.env.example` do `.env` i wpisz `ANTHROPIC_API_KEY`. Hasło `MSSQL_SA_PASSWORD` możesz zostawić puste: skrypt wygeneruje losowe przy pierwszym uruchomieniu. (Jeśli pominiesz ten krok, skrypt sam utworzy `.env`, ale bez klucza API generowanie przepisów nie zadziała.)
3. Zbuduj obraz i uruchom stos:
   ```bash
   ./scripts/local-prod.ps1
   ```
   Skrypt czeka, aż `/healthz` odpowie, i wypisuje adres aplikacji: http://localhost:8090.
4. Zarejestruj konto i zaloguj się (bez potwierdzania e-mailem).

Inne warianty skryptu:

```bash
./scripts/local-prod.ps1 -NoBuild   # restart bez przebudowy obrazu
./scripts/local-prod.ps1 -Reset     # usuwa wolumen bazy; schemat odtwarzają migracje
./scripts/local-prod.ps1 -Down      # zatrzymuje stos, dane zostają
```

Na macOS/Linux uruchom skrypt przez `pwsh ./scripts/local-prod.ps1`.

## Testy

Testy .NET używają SQLite w pamięci, więc nie potrzebują bazy ani klucza API:

```bash
dotnet test KitchenAssistant.slnx
```

Pojedynczy test: `dotnet test --filter "FullyQualifiedName~<Nazwa>"`.

Testy Playwright (wymagają Node.js, działającej aplikacji i kont testowych opisanych w `.env.example`):

```bash
npm install
```

```bash
npx playwright install
```

```bash
npm run e2e
```

Szczegóły: [context/foundation/test-stack.md](context/foundation/test-stack.md).

## Konfiguracja

| Klucz | Gdzie | Opis |
| --- | --- | --- |
| `ConnectionStrings:DefaultConnection` | `appsettings.json`, user-secrets, zmienne środowiskowe | Połączenie z SQL Server. Domyślnie LocalDB. |
| `ANTHROPIC_API_KEY` | user-secrets (dev), `.env` (Docker), App Settings (Azure) | Klucz API Anthropic. Bez niego aplikacja startuje z ostrzeżeniem, ale generowanie AI zwraca błąd. |
| `Recipes:Generator` | `appsettings*.json` | `Anthropic` albo `Fake` (`Fake` dozwolony tylko w Development). |
| `Recipes:Model`, `Recipes:Effort`, `Recipes:DeadlineSeconds` | `appsettings.json` | Model, poziom effort i limit czasu generowania (domyślnie 60 s). |
| `Database:MigrateOnStartup` | `appsettings.Production.json` | Stosuje migracje przy starcie aplikacji. |
| `Identity:RequireConfirmedAccount` | `appsettings*.json` | Wymaganie potwierdzenia konta (w Production wyłączone). |

`/healthz` to endpoint liveness: nie odpytuje bazy danych.

## Wdrożenie

Docelowo Azure App Service (Linux) + Azure SQL, z wdrożeniem przez GitHub Actions po merge do `main`. Każdy pull request przechodzi przez [CI](.github/workflows/ci.yml): build, kontrolę niezastosowanych zmian modelu EF, testy i audyt podatnych pakietów. Plan wdrożenia: [context/changes/deployment/deployment-plan.md](context/changes/deployment/deployment-plan.md).
