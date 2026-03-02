# DESIGN.md

## Project: AI Wars Idle (MVP v1)

---

# 0. Elevator Pitch

**AI Wars Idle** to idle economy game, w której gracze rozwijają “moc obliczeniową” i przejmują **sektory na mapie świata** w async PvP.
**Liga (sezon) trwa 30 dni**, a **mapa PvP resetuje się co 7 dni** (krótsze “wojny o mapę” w ramach sezonu ligi).

---

# 1. Cel MVP (walidacja)

Zweryfikować:

* D1 ≥ 35%
* D7 ≥ 12%
* ≥ 3% konwersji na płacących
* PvP usage (mapa) ≥ 40% aktywnych graczy
* Czy PvP terytorialne zwiększa ARPPU vs wersja bez PvP

MVP nie ma być „dużą grą”.
Ma sprawdzić, czy pętla **idle + konflikt terytorialny + sezon** działa.

---

# 2. Core Loop

1. Wejście → odbiór offline produkcji
2. Upgrade generatorów
3. Krótka aktywność (Overclock)
4. Mapa → sprawdzenie stanu sektorów (atak / obrona)
5. 1–5 akcji PvP (przejęcie sektora lub próba odbicia)
6. Zbliżenie do prestiżu + ustawienie kolejnego celu (sektor / próg prestiżu)

Sesja docelowa: 60–90 sekund.

---

# 3. Scope MVP (zamrożony)

## 3.1 Idle System

* 5 generatorów
* Produkcja pasywna (tick co sekundę)
* Overclock = aktywny boost (zamiast tap boost)
* Wykładniczy wzrost kosztów
* Globalny multiplier (z prestiżu)
* Offline progression (cap 12h)

---

## 3.2 Prestige

Warunek: osiągnięcie progu produkcji całkowitej.

Efekt:

* Reset generatorów
* Przyznanie “Shards” (permanentny progres)
* Stały globalny bonus (% produkcji)

Brak rozbudowanego drzewka.
Tylko 1 linia permanent upgrade (max 10 poziomów).

---

## 3.3 Map PvP (Async, MVP)

PvP w MVP to przejmowanie i utrzymywanie sektorów na mapie świata (2D).
Nie ma real-time, nie ma czatu, nie ma gildii.

### Mapa (UI i dane)

* 1 ekran mapy
* 20–30 sektorów (duże regiony, nie miasta)
* Zasada mapy: **brak teleportu** — atakować możesz tylko sektory sąsiednie (połączenia definiuje `MapConfig`)
* Sektor ma:
  * nazwę
  * % bonusu do globalnej produkcji (mały, ale odczuwalny)
  * właściciela (kolor)
  * `Stability` (0–100)
  * cooldown ataku (czas do kolejnej próby, jeśli dotyczy)

Kliknięcie sektora otwiera panel:

* właściciel + liga
* bonus sektora
* Twoja przybliżona szansa wygranej (widełki)
* wybór strategii + przycisk ataku

### Snapshot (źródło “mocy”)

Snapshot przechowuje minimum do rozstrzygnięcia walki:

* `PvpPower` (stat do walki; nie to samo co `ProductionPps` w ekonomii; sub ×2 nie działa bezpośrednio na `PvpPower`)
* liga / punkty sezonu
* (opcjonalnie MVP) znacznik aktywności (np. lastSeen) do UI/telemetrii

### Stability (anty-flip)

`Stability` ma zapobiegać flipowaniu sektorów co minutę:

* Po przejęciu sektor startuje z niską stabilnością (łatwy do odbicia).
* Z czasem stabilność rośnie (config).
* Im wyższa stabilność, tym silniejsza obrona sektora.

### Akcje PvP (limity)

* Ataki: **max 5**.
* Regeneracja: **1 atak co 2h**, do capu 5.
* Rewarded ad: **+1 atak / dzień**.
* Premium: możliwość dokupienia dodatkowych ataków za walutę premium (bez nielimitowanej energii w MVP).

### Strategie (decyzja przed atakiem)

Gracz wybiera 1 z 3 strategii (proste multipliery, config):

* **Agresywna**: wyższa szansa przejęcia, ale niski startowy `Stability` po wygranej.
* **Stabilna**: zbalansowana szansa i średni `Stability` po wygranej.
* **Ryzykowna**: najlepsza potencjalna nagroda, największa kara przy porażce.

### Symulacja walki (MVP)

Rozstrzygnięcie jest szybkie i czytelne:

**Atak:**
`AttackRoll = AttackerPvpPower × StrategyMultiplier × Random(0.95–1.05)`

**Obrona:**
`DefenseRoll = DefenderPvpPower × StabilityMultiplier(Stability) × Random(0.95–1.05)`

Jeśli `AttackRoll > DefenseRoll` → sektor zmienia właściciela.

### Wynik i nagrody

* **Wygrana ataku (przejęcie):**
  * przejęcie sektora
  * przyznanie soft currency
  * punkty sezonu / ligi
* **Przegrana ataku:**
  * mała nagroda soft currency (żeby nie bolało “nic”)
  * mała zmiana punktów (config)
  * (opcjonalnie MVP) krótki cooldown na ponowny atak tego samego sektora

### Minimalne reguły anty-snowball (MVP)

* Całkowity bonus z sektorów ma **cap** (config) lub **diminishing returns** po przekroczeniu progu.
* Najlepsze sektory nie powinny być dostępne wyłącznie dla top ligi w MVP (unikamy “murów”).

### Ligi i leaderboard

* Bronze / Silver / Gold / Diamond / Apex
* Sezon ligi: 30 dni (reset punktów i lig)
* Reset mapy PvP: co 7 dni (reset ownership i stability)
* Reset punktów po sezonie
* Leaderboard: Top 100 (MVP może być mock/lokalny; docelowo backend)

Ważne: **Liga ≠ ilość sektorów**. Liga wynika z punktów sezonu, a sektory są “widoczną wojną” na mapie.

---

## 3.4 Monetyzacja

### Subskrypcja (1 plan)

* +100% produkcji
* brak reklam przerywających (interstitial); rewarded ads zostają (opcjonalne)

Cena: TBD (test 9.99 / 14.99)

---

### Reklamy rewarded

* x2 offline claim
* +1 dodatkowy atak PvP dziennie

Brak:

* battle pass
* gacha
* dynamicznych ofert
* bundle 99+ USD

---

## 3.5 Event (1 template)

* 72h timer
* Pasek progresu (np. total production during event)
* 1 kosmetyczna nagroda

Bez osobnej waluty.

---

# 4. Ekrany

* Hub
* Generatory
* Mapa PvP
* Prestige (modal)
* Sklep
* Event (modal)

Zero zagnieżdżonych menu.
Max 2 tapnięcia do każdej funkcji.

---

# 5. Ekonomia

Waluty:

* Soft Currency
* Premium Currency
* Season Points (liga)

Sinki:

* Upgrade generatorów
* Permanent prestige upgrade
* (MVP) brak sinków stricte PvP poza “czasem/limitami”

Offline cap: 12h
Progression tempo:

* pierwszy prestige ≤ 30 min aktywnej gry

---

# 6. Architektura (wysoki poziom)

## Core Services

* EconomyService
* ProductionService
* UpgradeService
* PrestigeService
* GameState

## PvP

* SnapshotService
* MapService (sektory, ownership, stability)
* MatchmakingService (jeśli potrzeba do owner snapshotów w MVP)
* BattleSimService
* LeagueService
* LeaderboardService

## System

* Save/Load (wersjonowany SaveData)
* BalanceConfig (ScriptableObject)
* AnalyticsService

UI nie zawiera logiki biznesowej.

---

# 7. Telemetria (MUST)

Eventy:

* session_start
* offline_claim
* generator_upgrade
* prestige
* map_open
* sector_view
* sector_attack (sectorId, strategy)
* sector_result (win, sectorId)
* sector_ownership_changed (from, to) — jeśli jest w MVP
* ad_watched
* subscription_started

Bez analityki MVP jest bezwartościowe.

---

# 8. BAN LIST (nie robić w MVP)

* Artefakty z rerollami
* Gacha
* Tarcze PvP
* Obrona baz / budowanie baz
* Gildie / sojusze / czat
* Battle pass
* Dynamiczne oferty
* VIP 49+
* Real-time PvP
* Więcej niż 1 event template

Każda próba dodania czegoś z tej listy wymaga osobnego uzasadnienia metrykami.

---

# 9. Kryterium przejścia do v2

Przechodzimy do rozbudowy tylko jeśli:

* D7 ≥ 12%
* PvP usage ≥ 40%
* ≥ 3% konwersji
* ARPDAU rośnie po włączeniu PvP

Jeśli nie – poprawiamy core, nie dokładamy feature’ów
