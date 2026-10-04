---
project: "Kitchen Assistant"
version: 2
status: draft
created: 2026-09-24
updated: 2026-10-04
context_type: greenfield
product_type: web-app
target_scale:
  users: medium
timeline_budget:
  mvp_weeks: 3
  hard_deadline: 2026-10-22
  after_hours_only: true
---

## Vision & Problem Statement

Osoba gotująca dla siebie traci czas na szukanie przepisów, które pasują do składników już obecnych w lodówce i zapasach. Sytuacja występuje co najmniej raz w tygodniu i prowadzi do paraliżu decyzyjnego.

Zwykła wyszukiwarka prowadzi od wybranego przepisu do listy zakupów. Ta aplikacja wykorzystuje stan lodówki i zapasów, aby prowadzić użytkownika od posiadanych składników do przepisu.

## User & Persona

### Primary persona

Autor pomysłu, gotujący dla siebie i wybierający posiłek co najmniej raz w tygodniu. Pierwsza wersja może służyć również jego najbliższemu otoczeniu — łącznie maksymalnie kilkunastu osobom.

## Success Criteria

### Primary

- Zalogowany użytkownik korzysta z automatycznie utworzonej prywatnej przestrzeni, wpisuje produkty z lodówki oraz opcjonalnie zapasy, wybiera parametry posiłku i otrzymuje listę przepisów, w tym propozycje z niewielką listą brakujących składników. Może otworzyć szczegóły wybranego przepisu.

### Secondary

- Użytkownik może głosowo wprowadzać produkty do swojej przestrzeni.

### Guardrails

- Pełny przepis jest dostępny dla użytkownika w czasie nie dłuższym niż jedna minuta.
- Treść przepisu ma konsekwentną, ustrukturyzowaną postać, którą interfejs może czytelnie przedstawić.

## User Stories

### US-01: Użytkownik otrzymuje dopasowane przepisy

- **Given** zalogowanego użytkownika z dodanymi produktami
- **When** użytkownik prosi o podanie przepisów z wprowadzonymi parametrami
- **Then** otrzymuje kilka przepisów zawierających produkty użytkownika, wraz z przepisami z małą listą braków

#### Acceptance Criteria

- Przepis zawiera produkty, które posiada użytkownik.
- Przepis ma logiczne kroki przygotowania.

## Functional Requirements

- FR-001: Użytkownik może założyć konto i zalogować się. Priority: must-have
  > Socrates: Kontrargument rozważony: brak. Rozstrzygnięcie: funkcja zostaje.
- FR-002: Użytkownik może ręcznie dodać produkt do kategorii „zużyj w pierwszej kolejności” albo „w szafkach i zamrażalniku”. Priority: must-have
  > Socrates: Kontrargument rozważony: podział na lodówkę i zapasy jest zbyt szczegółowy. Rozstrzygnięcie: kategorie zmienione na „zużyj w pierwszej kolejności” oraz „w szafkach i zamrażalniku”.
- FR-003: Użytkownik może zmienić albo usunąć produkt. Priority: must-have
  > Socrates: Kontrargument rozważony: edycję i usuwanie można odłożyć. Rozstrzygnięcie: funkcja zostaje, ponieważ stan produktów szybko przestaje być aktualny.
- FR-004: Użytkownik może wybrać parametry posiłku z małego, zdefiniowanego zestawu. Priority: must-have
  > Socrates: Kontrargument rozważony: zbyt wiele parametrów utrudnia wybór. Rozstrzygnięcie: zestaw parametrów będzie mały i zdefiniowany.
- FR-005: Użytkownik może otrzymać listę dopasowanych przepisów, także z małą listą brakujących składników. Przepisy są generowane przez AI na podstawie produktów użytkownika i wybranych parametrów posiłku, a nie wyszukiwane w gotowej bazie. Priority: must-have
  > Socrates: Kontrargument rozważony: brak. Rozstrzygnięcie: funkcja zostaje; źródłem przepisów jest generowanie przez AI.
- FR-006: Użytkownik może otworzyć szczegóły przepisu. Priority: must-have
  > Socrates: Kontrargument rozważony: brak. Rozstrzygnięcie: funkcja zostaje.

## Non-Functional Requirements

- Pełny przepis jest dostępny dla użytkownika w czasie nie dłuższym niż jedna minuta.
- Treść przepisu ma konsekwentną, ustrukturyzowaną postać, którą interfejs może czytelnie przedstawić.

## Business Logic

Aplikacja najpierw pokazuje przepisy zgodne z wybranymi parametrami, które wykorzystują najwięcej produktów oznaczonych do szybkiego zużycia i nie wymagają zakupów, a następnie przepisy z małą liczbą brakujących składników, których wyższa widoczna ocena oznacza pełniejszy przepis z mniejszą liczbą braków.

Propozycje przepisów tworzy AI na podstawie produktów użytkownika i wybranych parametrów posiłku; reguła oceny i kolejności ma zastosowanie do wygenerowanych propozycji. Ocenę, liczbę brakujących składników i kolejność wylicza aplikacja, porównując listę składników każdego wygenerowanego przepisu z produktami użytkownika — nie przyjmuje ich od AI.

Reguła wykorzystuje produkty użytkownika w kategoriach „zużyj w pierwszej kolejności” oraz „w szafkach i zamrażalniku”, a także wybrane przez niego parametry posiłku.

Użytkownik prosi o przepisy i otrzymuje uporządkowaną listę propozycji wraz z widoczną oceną jakości. Najpierw widzi propozycje niewymagające zakupów, a później propozycje z niewielką liczbą braków.

Parametry posiłku to: rodzaj posiłku (śniadanie, obiad, kolacja, przekąska), maksymalny czas przygotowania (do 15, do 30, do 60 minut, bez limitu) oraz liczba porcji (1, 2, 4). Każdy parametr ma wartość domyślną (obiad, do 30 minut, 1 porcja), więc użytkownik może poprosić o przepisy bez zmieniania parametrów. Jedno zapytanie zwraca do 5 propozycji.

AI otrzymuje produkty użytkownika wraz z ich identyfikatorami. Dla składnika pochodzącego z produktów użytkownika zwraca identyfikator tego produktu, a brakującemu składnikowi nadaje nazwę. Aplikacja uznaje składnik za posiadany tylko wtedy, gdy zwrócony identyfikator istnieje na liście produktów tego użytkownika.

Składniki ze stałej listy „zawsze w domu” — sól, olej, woda oraz typowe suszone przyprawy (np. pieprz, papryka słodka, oregano, cynamon) — nie są liczone jako braki i nie wchodzą do oceny. Lista jest zamknięta i utrzymywana w aplikacji; przyprawa spoza listy jest zwykłym składnikiem (decyzja 2026-10-04, rozszerzenie o przyprawy).

„Mała lista braków” oznacza maksymalnie 2 brakujące składniki. AI otrzymuje ten sam limit, a aplikacja odrzuca propozycje, które go przekraczają.

Widoczna ocena ma postać „Masz X z Y składników” (bez składników „zawsze w domu”) wraz z liczbą brakujących składników. Propozycja pokazuje także, ile produktów „zużyj w pierwszej kolejności” wykorzystuje.

## Access Control

Użytkownik zakłada konto z e-mailem i hasłem. W MVP każdy użytkownik ma własną, prywatną przestrzeń z lodówką i zapasami; zaproszenia i współdzielenie przestrzeni są poza zakresem MVP.

## Non-Goals

- Zdjęcia i głosowe wprowadzanie produktów nie należą do MVP; produkty są wprowadzane ręcznie.
- Wspólne lodówki, zaproszenia i współdzielenie przestrzeni nie należą do MVP; dane użytkownika są prywatne.
- Akcja „ugotuję to” i automatyczne uszczuplanie zapasów nie należą do MVP.
- Ulubione przepisy i historia przygotowanych dań nie należą do MVP.
- Tryb offline i osobna aplikacja mobilna nie należą do MVP.

## Open Questions

Brak otwartych pytań. Pytania z wersji 1 rozstrzygnięto 2026-09-30, a decyzje zapisano w *Business Logic*:

1. Zestaw parametrów posiłku — rodzaj posiłku, maksymalny czas, liczba porcji, z wartościami domyślnymi.
2. „Mała lista braków” — maksymalnie 2 brakujące składniki; propozycje ponad limit są odrzucane.
3. Dopasowanie składników — AI zwraca identyfikator produktu użytkownika zamiast nazwy; składniki „zawsze w domu” nie są brakami.
