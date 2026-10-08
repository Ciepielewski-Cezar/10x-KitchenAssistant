---
project: Kitchen Assistant
version: 1
status: draft
created: 2026-09-30
updated: 2026-10-08
prd_version: 2
main_goal: speed
top_blocker: time
milestone_id: mvp-recipes-from-pantry
milestone_seq: 1
milestone_status: open
---

# Roadmap: Kitchen Assistant

> Derived from `context/foundation/prd.md` (v2) + auto-researched codebase baseline.
> Edit-in-place; archive when superseded.
> Slices below are listed in dependency order. The "At a glance" table is the index.

## Milestone

**M-1: MVP — przepisy z tego, co mam** — Status: open

- **Intent:** Zalogowany użytkownik prowadzi prywatną listę produktów, prosi o przepisy z wybranymi parametrami i dostaje uporządkowaną listę propozycji wygenerowanych przez AI (także z małą listą braków), a potem otwiera szczegóły wybranej. Aplikacja działa na produkcji dla autora i jego najbliższego otoczenia.
- **Source materials:** `context/foundation/prd.md` (v2)
- **Done when:** every F-NN and S-NN below is `done`, plus pełny przepływ z Kryterium sukcesu (Primary) w PRD działa na środowisku produkcyjnym.
- **Scope anchors:** FR-001 … FR-006 (wszystkie konieczne), US-01, Business Logic, Non-Functional Requirements („NFR ≤ 1 min” = pełny przepis w ≤ 1 min; „NFR struktura” = spójna, ustrukturyzowana postać przepisu), Access Control.

## Vision recap

Osoba gotująca dla siebie co najmniej raz w tygodniu traci czas na szukanie przepisu, który pasuje do tego, co już ma w lodówce i szafkach. Zwykła wyszukiwarka prowadzi od przepisu do listy zakupów. Kitchen Assistant odwraca ten kierunek: zaczyna od posiadanych składników i prowadzi do przepisu wygenerowanego przez AI, przy czym to aplikacja, a nie AI, ocenia i porządkuje propozycje.

## North star

**S-02: Użytkownik z zapisanymi produktami prosi o przepisy i w ciągu minuty dostaje kilka propozycji wygenerowanych przez AI z jego produktów** — to najmniejszy przepływ, który sprawdza najbardziej ryzykowne założenie (jakość i czas odpowiedzi AI), więc przy celu `speed` idzie zaraz po pierwszym kawałku, który daje mu dane wejściowe.

> „North star” (gwiazda przewodnia) oznacza tu najmniejszy kawałek od początku do końca, którego dowiezienie dowodzi, że produkt w ogóle działa. Stawiamy go tak wcześnie, jak pozwalają zależności, bo reszta ma sens tylko wtedy, gdy on działa.

## At a glance

| ID   | Change ID               | Outcome (user can …)                                                                                           | Prerequisites | PRD refs                                   | Status   |
| ---- | ----------------------- | -------------------------------------------------------------------------------------------------------------- | ------------- | ------------------------------------------ | -------- |
| F-01 | deployment              | (foundation) aplikacja działa na produkcji z alertami o błędach, a merge do `main` wdraża ją automatycznie        | —             | frontmatter `timeline_budget`, NFR ≤ 1 min | ready    |
| S-01 | pantry-add-products     | po zalogowaniu widzi swoją prywatną listę produktów i dodaje produkt do jednej z dwóch kategorii                | —             | FR-001, FR-002, US-01, Access Control      | done        |
| S-02 | first-recipe-generation | prosi o przepisy i w ciągu minuty dostaje kilka propozycji wygenerowanych przez AI z jego produktów             | S-01, lokalny klucz API dostawcy AI (krok L2 planu local-dev) | US-01, FR-005, NFR ≤ 1 min, NFR struktura  | done |
| S-03 | recipe-ranking          | widzi propozycje uporządkowane według oceny i liczby braków, które liczy aplikacja                              | S-02          | FR-005, US-01, Business Logic              | proposed |
| S-04 | recipe-details          | otwiera szczegóły wybranej propozycji i widzi pełny przepis w spójnej strukturze                                | S-02, S-07    | FR-006, NFR struktura, NFR ≤ 1 min         | proposed |
| S-05 | meal-parameters         | wybiera parametry posiłku z małego zestawu, a propozycje je respektują                                          | S-02          | FR-004, US-01, Business Logic              | proposed |
| S-06 | pantry-edit-remove      | zmienia albo usuwa produkt ze swojej listy                                                                      | S-01          | FR-003                                     | proposed |
| S-07 | recipe-generation-spike | dostaje propozycje w ≤ 1 min na ustawieniach modelu potwierdzonych pomiarem 10 prawdziwych wywołań              | S-02, lokalny klucz API z limitem wydatków | NFR ≤ 1 min, FR-005, NFR struktura | ready |

## Streams

Navigation aid — groups items that share a Prerequisites chain. Canonical ordering still lives in the dependency graph below; this table is the proposed reading order across parallel tracks.

| Stream | Theme                   | Chain                  | Note                                                                                              |
| ------ | ----------------------- | ---------------------- | ------------------------------------------------------------------------------------------------- |
| A      | Wdrożenie produkcyjne   | `F-01`                 | Niezależny od funkcji; prowadzony równolegle od początku, bo głównym ryzykiem jest czas.          |
| B      | Produkty użytkownika    | `S-01` → `S-06`        | `S-01` jest wejściem dla strumienia C; `S-06` można robić w dowolnym momencie po `S-01`.          |
| C      | Pierwszy dowód i ocena  | `S-02` → `S-03` · `S-07` | Dołącza do strumienia B w `S-01`; ścieżka konieczna do MVP przy celu `speed`. `S-07` (pomiar wydzielony z `S-02`) jest niezależny od `S-03`. |
| D      | Doprecyzowanie przepisu | `S-04` · `S-05`        | Dołącza do strumienia C w `S-02`; oba kawałki są od siebie i od `S-03` niezależne. `S-04` czeka na werdykt `S-07`. |

## Baseline

What's already in place in the codebase as of `2026-09-30` (auto-researched + user-confirmed).
Foundations below assume these are present and do NOT re-scaffold them.

- **Frontend:** present — aplikacja z renderowaniem wybieranym per strona (`Components/App.razor`); są tylko strony przykładowe z szablonu, brak stron produktowych.
- **Backend / API:** present — jeden proces (`Program.cs`); klient dostawcy AI zarejestrowany z limitem 60 s, ale brak usługi generowania przepisów.
- **Data:** partial — baza, mapowanie i migracje działają (tożsamość, klucze ochrony danych w `Data/Migrations/`); brak encji domenowych (produkty, przepisy) w `Data/`.
- **Auth:** present — konta e-mail + hasło, logowanie ciasteczkiem (`Program.cs`, `Components/Account/`).
- **Deploy / infra:** partial — lokalny stos zbliżony do produkcji działa (`compose.yaml`, `scripts/local-prod.ps1`); plan wdrożenia (`context/changes/deployment/deployment-plan.md`) spisany, faza 2 wykonana, fazy 0, 1 i 3–6 nie; brak pipeline'u CI/CD.
- **Observability:** partial — domyślne logowanie + `/healthz` (tylko liveness); brak monitoringu błędów produkcji.

## Foundations

### F-01: Pierwsze wdrożenie produkcyjne

- **Outcome:** (foundation) obecny szkielet aplikacji działa na produkcji z bazą danych i sekretami poza repozytorium, z alertami o niedostępności, błędach żądań i przekroczeniu budżetu, a merge do `main` wdraża go automatycznie.
- **Change ID:** deployment
- **PRD refs:** frontmatter `timeline_budget`, NFR ≤ 1 min
- **Unlocks:** ścieżka weryfikacji „każdy kawałek sprawdzany na produkcji po merge” dla `S-01`…`S-06`; weryfikacja wymogu ≤ 1 min dla `S-02` na docelowej infrastrukturze (telemetria czasu żądań); alert na błędy żądań wyłapuje awarie bazy i generowania AI, których test `/healthz` nie widzi; zmniejsza ryzyko z `infrastructure.md` „konfiguracja tożsamości i zasobów przekracza budżet czasu”.
- **Prerequisites:** —
- **Parallel with:** S-01, S-02, S-03, S-04, S-05, S-06
- **Blockers:** uaktualnienie subskrypcji do płatnej i przydział limitu dla planu B1 (faza 0 planu wdrożenia, wykonuje właściciel); produkcyjny klucz API dostawcy AI.
- **Unknowns:** —
- **Risk:** plan już istnieje, a zakres jest ograniczony do wdrożenia obecnego szkieletu z minimalnym monitoringiem (faza 8 planu + alert na błędy — decyzja 2026-09-30); stoi na początku, bo konfiguracja chmury to miejsce, gdzie znikają dni, a przy celu `speed` wdrożenie na końcu grozi przekroczeniem terminu.
- **Status:** ready

## Slices

### S-01: Prywatna lista produktów — dodawanie

- **Outcome:** użytkownik po zalogowaniu widzi swoją prywatną listę produktów (tworzoną automatycznie, bez żadnej konfiguracji) i dodaje produkt do kategorii „zużyj w pierwszej kolejności” albo „w szafkach i zamrażalniku”.
- **Change ID:** pantry-add-products
- **PRD refs:** FR-001, FR-002, US-01, Access Control
- **Prerequisites:** —
- **Parallel with:** F-01
- **Blockers:** —
- **Unknowns:** —
- **Risk:** pierwsza encja domenowa i pierwsze wymuszenie prywatności danych per użytkownik; stoi pierwszy, bo zarówno generowanie przepisów, jak i edycja produktów potrzebują tej listy.
- **Status:** done

### S-02: Pierwsze generowanie przepisów

- **Outcome:** użytkownik z zapisanymi produktami prosi o przepisy i w ciągu minuty dostaje do 5 propozycji wygenerowanych przez AI, zbudowanych z jego produktów, każdą z listą składników i krokami przygotowania; składnik z zapasów jest powiązany z produktem użytkownika przez identyfikator (bez oceny i kolejności — te dochodzą w `S-03`).
- **Change ID:** first-recipe-generation
- **PRD refs:** US-01, FR-005, NFR ≤ 1 min, NFR struktura
- **Prerequisites:** S-01, lokalny klucz API dostawcy AI (krok L2 planu local-dev)
- **Parallel with:** F-01, S-06
- **Blockers:** —
- **Unknowns:** — (pytanie o mieszczenie się 5 przepisów w minucie przeniesione 2026-10-08 do `S-07` razem z pomiarem i `spike.md`)
- **Risk:** najbardziej ryzykowne założenie produktu (jakość i czas odpowiedzi AI) — stoi zaraz po `S-01`, żeby ewentualna zmiana podejścia wyszła, zanim powstanie reszta.
- **Status:** done

### S-03: Kolejność i ocena propozycji

- **Outcome:** użytkownik widzi propozycje uporządkowane: najpierw niewymagające zakupów i zużywające najwięcej produktów „zużyj w pierwszej kolejności”, potem te z 1–2 brakami; przy każdej widzi ocenę „Masz X z Y składników” i liczbę braków, które wylicza aplikacja, a nie AI; propozycje z więcej niż 2 brakami nie są pokazywane.
- **Change ID:** recipe-ranking
- **PRD refs:** FR-005, US-01, Business Logic
- **Prerequisites:** S-02
- **Parallel with:** F-01, S-04, S-05, S-06
- **Blockers:** —
- **Unknowns:** —
- **Risk:** poprawność oceny to twarda reguła produktu (liczy ją aplikacja, nigdy AI); reguła jest już w pełni określona w PRD v2 (maks. 2 braki, odrzucanie ponad limit, lista „zawsze w domu”, ocena „Masz X z Y”), więc da się ją zweryfikować testami bez AI.
- **Status:** proposed

### S-04: Szczegóły przepisu

- **Outcome:** użytkownik otwiera wybraną propozycję i widzi pełny przepis (składniki, kroki) w spójnej, czytelnej strukturze.
- **Change ID:** recipe-details
- **PRD refs:** FR-006, NFR struktura, NFR ≤ 1 min
- **Prerequisites:** S-02, S-07 (werdykt pomiaru decyduje, czy potrzebne jest drugie wywołanie AI)
- **Parallel with:** F-01, S-03, S-05, S-06
- **Blockers:** —
- **Unknowns:**
  - Czy wygenerowane propozycje muszą przetrwać odświeżenie strony lub ponowne logowanie, czy wystarczy bieżąca sesja? (PRD wyłącza historię z MVP, co sugeruje sesję.) — Owner: user. Block: no.
- **Risk:** niskie ryzyko; zależy od kształtu przepisu ustalonego w `S-02` i potwierdzonego w `S-07` — jeśli `S-07` każe rozdzielić generowanie na listę i pełny przepis, tu ląduje drugie wywołanie AI i wymóg ≤ 1 min.
- **Status:** proposed

### S-05: Parametry posiłku

- **Outcome:** użytkownik przed prośbą o przepisy zmienia rodzaj posiłku, maksymalny czas lub liczbę porcji (domyślnie: obiad, do 30 min, 1 porcja), a propozycje są z nimi zgodne.
- **Change ID:** meal-parameters
- **PRD refs:** FR-004, US-01, Business Logic
- **Prerequisites:** S-02
- **Parallel with:** F-01, S-03, S-04, S-06
- **Blockers:** —
- **Unknowns:** —
- **Risk:** rozszerza prośbę o przepisy z `S-02`, więc przy równoległej pracy z `S-03` oba kawałki dotykają tego samego przepływu — kolejność scalania ustalić w planach.
- **Status:** proposed

### S-06: Zmiana i usuwanie produktu

- **Outcome:** użytkownik zmienia nazwę lub kategorię produktu albo usuwa produkt ze swojej listy.
- **Change ID:** pantry-edit-remove
- **PRD refs:** FR-003
- **Prerequisites:** S-01
- **Parallel with:** F-01, S-02, S-03, S-04, S-05
- **Blockers:** —
- **Unknowns:** —
- **Risk:** niskie ryzyko i brak zależnych kawałków; stoi za pierwszym dowodem, bo przy celu `speed` nie powinien go opóźniać, ale jest konieczny, bo stan produktów szybko się dezaktualizuje.
- **Status:** proposed

### S-07: Pomiar generowania i ustawienia modelu

- **Outcome:** użytkownik dostaje propozycje w ≤ 1 min na ustawieniach modelu (`Recipes:Model` / `Recipes:Effort`) potwierdzonych pomiarem co najmniej 10 prawdziwych wywołań; czas, tokeny, jakość i poprawność identyfikatorów produktów są zapisane w `spike.md`.
- **Change ID:** recipe-generation-spike
- **PRD refs:** NFR ≤ 1 min, FR-005, NFR struktura
- **Prerequisites:** S-02, lokalny klucz API dostawcy AI z limitem wydatków w konsoli
- **Parallel with:** F-01, S-03, S-05, S-06
- **Blockers:** —
- **Unknowns:**
  - Czy wygenerowanie 5 pełnych przepisów w jednym wywołaniu mieści się w minucie, czy trzeba to rozdzielić (lista propozycji, potem pełny przepis)? — Owner: team. Block: no.
    - Wstępnie (2026-10-05): jedno wywołanie (Sonnet 5.5 / low) — 14,5 s i 5 propozycji w smoke teście na stosie produkcyjnym (`context/changes/recipe-generation-spike/spike.md`).
- **Risk:** wydzielony 2026-10-08 z fazy 3 `S-02`, żeby `S-02` mogło się zamknąć; jedyny krok, który kosztuje (ok. 0,30 USD za 11 wywołań); werdykt decyduje o kształcie `S-04`, więc musi przyjść przed jego planowaniem i przed MVP (2026-10-22).
- **Status:** ready

## Backlog Handoff

| Roadmap ID | Change ID               | Suggested issue title                                              | Ready for `/10x-plan` | Notes |
| ---------- | ----------------------- | ------------------------------------------------------------------ | --------------------- | ----- |
| F-01       | deployment              | Pierwsze wdrożenie produkcyjne (Azure + CI/CD)                     | yes                   | Plan już istnieje: `context/changes/deployment/deployment-plan.md` — kontynuuj od fazy 0. |
| S-01       | pantry-add-products     | Prywatna lista produktów: dodawanie do dwóch kategorii             | yes                   | Run `/10x-plan pantry-add-products` |
| S-02       | first-recipe-generation | Generowanie przepisów przez AI z produktów użytkownika             | no                    | Czeka na `S-01` i lokalny klucz API. |
| S-03       | recipe-ranking          | Kolejność, ocena i liczba braków liczone przez aplikację           | no                    | Czeka na `S-02`. |
| S-04       | recipe-details          | Szczegóły wygenerowanego przepisu                                  | no                    | Czeka na werdykt `S-07`. |
| S-05       | meal-parameters         | Wybór parametrów posiłku przy prośbie o przepisy                   | no                    | Czeka na `S-02`. |
| S-06       | pantry-edit-remove      | Zmiana i usuwanie produktu                                         | no                    | Czeka na `S-01`. |
| S-07       | recipe-generation-spike | Pomiar 10 wywołań AI i zatwierdzenie ustawień modelu               | yes                   | Plan i runbook gotowe: `context/changes/recipe-generation-spike/` — potrzebny klucz dev. |

## Open Roadmap Questions

Brak otwartych pytań. Rozstrzygnięte 2026-09-30:

- Parametry posiłku, próg braków, dopasowanie składników → PRD v2 § Business Logic (odblokowało `S-03` i `S-05`).
- Monitoring przed MVP → faza 8 planu wdrożenia + alert na błędy żądań, w zakresie `F-01`.

## Parked

- **Zdjęcia i głosowe wprowadzanie produktów** — Why parked: PRD §Non-Goals (głos to też kryterium drugorzędne, nie MVP).
- **Wspólne lodówki, zaproszenia i współdzielenie przestrzeni** — Why parked: PRD §Non-Goals; dane użytkownika są prywatne.
- **Akcja „ugotuję to” i automatyczne uszczuplanie zapasów** — Why parked: PRD §Non-Goals.
- **Ulubione przepisy i historia przygotowanych dań** — Why parked: PRD §Non-Goals.
- **Tryb offline i osobna aplikacja mobilna** — Why parked: PRD §Non-Goals.
- **Wysyłka e-maili (potwierdzenie konta, reset hasła)** — Why parked: FR-001 jej nie wymaga, na produkcji potwierdzanie konta jest wyłączone, a przy głównym ryzyku `time` to zbędny zakres.
- **Dieta jako parametr posiłku** — Why parked: decyzja 2026-09-30; to raczej cecha profilu użytkownika niż pojedynczej prośby, a zestaw parametrów ma pozostać mały (FR-004).
- **Natychmiastowy rollback i środowisko podglądu (sloty wdrożeniowe)** — Why parked: `infrastructure.md` — wymaga droższego planu; przy kilkunastu użytkownikach wystarcza ponowne wdrożenie poprzedniej wersji.

## Milestone History

## Done

- **S-01: użytkownik po zalogowaniu widzi swoją prywatną listę produktów (tworzoną automatycznie, bez żadnej konfiguracji) i dodaje produkt do kategorii „zużyj w pierwszej kolejności” albo „w szafkach i zamrażalniku”.** — Archived 2026-10-03 → `context/archive/2026-10-03-pantry-add-products/`. Lesson: —.
- **S-02: użytkownik z zapisanymi produktami prosi o przepisy i w ciągu minuty dostaje do 5 propozycji wygenerowanych przez AI, zbudowanych z jego produktów, każdą z listą składników i krokami przygotowania; składnik z zapasów jest powiązany z produktem użytkownika przez identyfikator (bez oceny i kolejności — te dochodzą w `S-03`).** — Archived 2026-10-08 → `context/archive/2026-10-03-first-recipe-generation/`. Lesson: —.
