# TASKS_delivery_core_first.md

## AI Wars Idle (MVP v1) — Delivery Plan (core-first, UI-last)

Cel: dowieźć działające MVP zgodne z `DESIGN.md` i `TECH_SPEC.md`, budując najpierw **czyste serwisy i modele**, a dopiero na końcu podpinając UI.

### Ustalenia projektu (pinned)
* Silnik: **Unity 6.3**
* UI tech: **uGUI** (Canvas-based). Powód: istnieją gotowe prefab’y pod uGUI.
* Platforma docelowa (MVP): **Android-only**
* Identyfikator aplikacji (Android package name):
  * Internal test: `com.aiwarsidle.game.dev`
  * Soft launch / prod: `com.aiwarsidle.game` (po testach usuwamy suffix `.dev`)
* Usługi backend/SDK: **Firebase**
  * Auth: **Anonymous** na MVP, w przyszłości **link do Google**
  * Analytics + Crashlytics + Remote Config: **włączone** (MVP i dalej)
  * Pod przyszły Cloud Save: **Firestore** (przechowywanie save jako wersjonowany payload)
* Save/Load (długofalowo + pod Cloud Save):
  * Format: **JSON (Newtonsoft)** + jawne `SaveDataV1`/wersjonowanie i migracje
  * Lokalnie: `Application.persistentDataPath` + zapis atomowy + backup (odporność na corrupt)
  * Cloud Save w przyszłości: ten sam JSON payload (lub skompresowany), sync per userId z Firebase Auth
* Czas i timestampy:
  * Jednostka: **UnixTimeSeconds (UTC)** w `long` (spójnie: offline, regen, stability, cooldowny)
* Typy liczb:
  * SoftCurrency i kalkulacje ekonomii: **double** (zgodnie z `TECH_SPEC.md`), unikać `float` w walutach
* Event system (domena → UI/telemetria):
  * W core: **lekki EventBus / domenowe eventy** (bez zależności od UI), testowalny przez fake/mock
* Walidacja configów:
  * ScriptableObject configi walidowane **również runtime (fail-fast)** + testy walidacji
* Resume / “single entry” przez Splash:
  * Jeśli app była w tle **>= 10 minut**, na powrót pokazujemy **Splash screen** (traktowany jako jedyne wejście do apki).
  * Jeśli app wraca szybciej niż 10 minut: **bez splash**, zostajemy w bieżącym stanie.
  * Po splash zawsze routing do: **Hub**.

Założenia testów:
* Unity Test Framework
* Preferuj **EditMode** dla serwisów bez MonoBehaviour.
* PlayMode tylko tam, gdzie potrzebujesz pętli gry / ticków / integracji Unity lifecycle.
* Każdy komponent ma: testy “happy path”, testy brzegowe, testy regresji dla najważniejszego buga/ryzyka.

---

# EPIC 0 — Repo + Fundamenty projektu

## STORY 0.1 — Konwencje, build, test harness

* [x] Ustalić foldery zgodnie z `TECH_SPEC.md` (`/GameCore`, `/PvP`, `/Persistence`, `/Data`, `/Analytics`, `/Monetization`, `/UI`)
* [x] Dodać assembly definition (asmdef) dla warstw core (żeby testy nie ciągnęły UI)
* [x] Skonfigurować Unity Test Runner (EditMode + PlayMode)

**TESTS**
* [x] Smoke test: uruchomienie EditMode + PlayMode test suite na local

---

# EPIC 1 — Modele domenowe + kontrakty

## STORY 1.1 — Modele i typy (bez logiki)

* [x] `GameState` (runtime) zgodnie z `TECH_SPEC.md`
* [x] `PvpSnapshot`, `BattleResult`
* [x] Map PvP modele: `MapState`, `SectorState`, `AttackStrategy`, `AttackPreview`, `CombatResult`
* [x] Overclock modele: `OverclockState` (ładunki, cooldown, aktywność)

**TESTS**
* [x] Test serializacji/roundtrip (jeśli modele będą serializowane) dla kluczowych pól
* [x] Testy walidacji zakresów: `Stability` zawsze 0..100, `SectorId` poprawny, itp.
* [x] Testy walidacji Overclock: charges 0..max, brak ujemnych timestampów

---

# EPIC 2 — Persistence (Save/Load) + migracje

## STORY 2.1 — SaveData schema v1

* [x] `SaveDataV1` zgodny z `TECH_SPEC.md` (waluty, generatory, prestige, liga, season points, ataki z regenem, `leagueSeasonId`, `mapSeasonId`, sektory)
* [x] `SectorSaveData` (właściciel, owner snapshot minimalny, stability, timestampy)
* [x] `OverclockSaveData` (ładunki, cooldown, aktywność)

**TESTS**
* [x] SaveDataV1: poprawne domyślne wartości (np. brak nulli, poprawne długości tablic)
* [x] Sektor: clamp stability, brak ujemnych timestampów
* [x] Overclock: poprawne wartości startowe (np. 2 ładunki, brak aktywności)

## STORY 2.2 — SaveService

* [ ] `SaveService.Load()` (tworzy nowe dane jeśli brak)
* [ ] `SaveService.Save()` (atomowy zapis; brak corruptu przy przerwaniu)
* [ ] Autosave co 30s + zapis on pause/quit (hook na bootstrapie, nie w UI)
* [ ] Wersjonowanie + migracje (stub pod v2, nawet jeśli MVP tylko v1)

**TESTS**
* [ ] Save→Load: stan identyczny (w tym sektory mapy)
* [ ] Test odporności: uszkodzony plik save → fallback do nowego save (lub backup) bez crasha

---

# EPIC 3 — Ekonomia idle (bez UI)

## STORY 3.1 — BalanceConfig (ScriptableObjects)

* [ ] `BalanceConfig` (koszty, produkcje, growth factors, prestige threshold base+growth, permanent upgrade cap, offline cap)
* [ ] Wartości przykładowe pod MVP (tylko do testów wewnętrznych)

**TESTS**
* [ ] Testy walidacji configu (np. 5 generatorów, brak zer/ujemnych growth factors)

## STORY 3.2 — EconomyService

* [ ] `AddCurrency(amount)` i `SpendCurrency(amount)` z walidacją
* [ ] Eventy domenowe (np. `CurrencyChanged`) bez zależności od UI

**TESTS**
* [ ] Spend > balance → fail (bez zmian stanu)
* [ ] Add/Spend z wartościami skrajnymi (0, bardzo małe, bardzo duże)

## STORY 3.3 — ProductionService

* [ ] `CalculateProductionPerSecond()`
* [ ] Tick co 1s (bez MonoBehaviour w serwisie; tick wywoływany przez bootstrap)
* [ ] Offline gain z capem 12h

**TESTS**
* [ ] PPS rośnie po upgrade
* [ ] Offline gain: cap działa (np. 20h → liczy tylko 12h)
* [ ] Determinizm: te same wejścia → te same wyniki

Uwaga: bonusy z sektorów + maintenance (anti-snowball) można spiąć po EPIC 6, żeby nie wprowadzać zależności Map→Core za wcześnie.

## STORY 3.4 — UpgradeService

* [ ] Koszt: `baseCost * pow(growthFactor, level)`
* [ ] `GetUpgradeCost(generatorId)` + `UpgradeGenerator(generatorId)`
* [ ] Obsługa x1/x10/max (logika, bez UI)

**TESTS**
* [ ] Koszt rośnie wykładniczo
* [ ] Upgrade bez środków → brak zmian
* [ ] Upgrade x10 nie przekracza limitów i liczy poprawnie sumaryczny koszt (jeśli implementowane)

## STORY 3.5 — PrestigeService

* [ ] `CanPrestige()` (threshold)
* [ ] `ExecutePrestige()` reset generatorów, inkrement permanent upgrade, zwiększ multiplier

**TESTS**
* [ ] Prestige resetuje tylko to co trzeba (waluty? generatory? zgodnie z designem)
* [ ] Permanent upgrade nie przekracza max 10 (clamp)

---

# EPIC 4 — Overclock (aktywny boost, core)

Overclock zastępuje klasyczne spam-tapowanie: to **taktyczny przycisk decyzji** używany przed PvP i w kluczowych momentach sesji.

Parametry MVP (config):
* czas trwania: 10s
* cooldown: 90s
* max ładunki: 2
* regen: 1 ładunek co 90s
* efekty: ×3 produkcja, +20% PvP Attack Power, +10% szybszy wzrost stability świeżo zdobytego sektora

## STORY 4.1 — OverclockConfig

* [ ] `OverclockConfig` (czas trwania, cooldown/regen, max charges, mnożniki)

**TESTS**
* [ ] Walidacja configu: czasy > 0, mnożniki w sensownych zakresach (guardrails)

## STORY 4.2 — OverclockService

* [ ] Stan runtime: ładunki, `ActiveUntil`, cooldown/regen
* [ ] `CanActivate(now)` + `Activate(now)`
* [ ] `Tick(now)` (regen ładunków, kończenie aktywności)
* [ ] Interfejs do innych serwisów:
  * `GetProductionMultiplier(now)` (×3 gdy aktywny)
  * `GetPvpAttackMultiplier(now)` (+20% gdy aktywny)
  * `GetFreshCaptureStabilityGrowthMultiplier(now)` (+10% gdy aktywny)

**TESTS**
* [ ] Activate zużywa ładunek i ustawia `ActiveUntil`
* [ ] Brak ładunków → nie da się aktywować
* [ ] Regen: 1 ładunek co 90s, clamp do max 2
* [ ] Cooldown/regen nie “stackuje się” i nie daje > max ładunków
* [ ] Multipliery działają tylko w oknie aktywności

## STORY 4.3 — Integracje (bez UI)

* [ ] `ProductionService` uwzględnia multiplier z Overclock (w kalkulacji PPS i ticku)
* [ ] `BattleSimService` uwzględnia bonus attack power z Overclock (na atakującym)
* [ ] `MapService.TickStability` uwzględnia +10% wzrostu stability dla **świeżo zdobytych** sektorów, gdy Overclock aktywny
  * “Świeżo zdobyty” = sektor przejęty w ostatnich `FreshCaptureWindowSeconds` (config)
* [ ] Telemetria: `overclock_activate`, `overclock_charge_spent`, `overclock_charge_gain`

**TESTS**
* [ ] Włączony Overclock → PPS rośnie ×3
* [ ] Włączony Overclock → wyższa szansa wygranej w identycznych warunkach (deterministyczny seed)
* [ ] Włączony Overclock → stability świeżo przejętego sektora rośnie szybciej (przy tym samym czasie)

---

# EPIC 5 — PvP: Snapshot + Ligi (bez mapy jeszcze)

## STORY 5.1 — SnapshotService

* [ ] Budowa `PvpSnapshot` z aktualnego `GameState`
* [ ] `PvpPower` liczony z osobnej statystyki (sub ×2 nie działa bezpośrednio)
  * core progression: prestige, permanent upgrades, sektory
  * + mały udział z produkcji przez soft-cap (z `BaseProductionPpsWithoutSubscription`)
* [ ] Konfiguracja w `PvpConfig`:
  * `PrestigePvpPowerPerPrestige`, `PermanentPvpPowerPerLevel`, `SectorPvpPowerPerSector`
  * `ProdToPvpMaxBonus`, `ProdToPvpHalfCapPps`

**TESTS**
* [ ] Snapshot `PvpPower` nie zmienia się po włączeniu subskrypcji (bezpośrednio)
* [ ] Snapshot `PvpPower` rośnie po prestiżu / permanent upgrade / sektorach

## STORY 5.2 — LeagueService (Season Points)

* [ ] Progi lig w `LeagueConfig`
* [ ] Dodawanie punktów sezonu + awans/spadek
* [ ] Reset sezonu ligi (30 dni; date-based / `leagueSeasonId`) — w logice domenowej, bez UI

**TESTS**
* [ ] Punkty → prawidłowa liga (progi)
* [ ] Reset sezonu ligi zeruje punkty i odświeża `leagueSeasonId`

## STORY 5.3 — PvpConfig (walidacja i guardrails)

* [ ] Walidacja `PvpConfig` (ScriptableObject) na starcie gry / w testach:
  * `PowerVarianceMin > 0` i `PowerVarianceMin <= PowerVarianceMax`
  * `StrategyMultiplier* > 0`
  * `StabilityMultiplierMin > 0` i `StabilityMultiplierMin <= StabilityMultiplierMax`
  * `PrestigePvpPowerPerPrestige >= 0`, `PermanentPvpPowerPerLevel >= 0`, `SectorPvpPowerPerSector >= 0`
  * `ProdToPvpMaxBonus >= 0` oraz `ProdToPvpMaxBonus <= 1` (max +100% do core)
  * `ProdToPvpHalfCapPps > 0`

**TESTS**
* [ ] Nieprawidłowy config → błąd walidacji (fail fast) lub bezpieczny fallback (zależnie od decyzji)
* [ ] Poprawny config → przechodzi walidację

---

# EPIC 6 — PvP: Mapa sektorów (core)

## STORY 6.1 — MapConfig

* [ ] `MapConfig` (reset mapy co 7 dni, lista sektorów + sąsiedztwo, bonusy produkcji + bonusy PvP, cap/diminishing returns, stability parametry, cooldown)
* [ ] `FreshCaptureWindowSeconds` (okno “świeżo zdobytego” sektora pod bonus Overclock)

**TESTS**
* [ ] Walidacja: 20–30 sektorów, unikalne `SectorId`, bonusy w sensownym zakresie
* [ ] Walidacja sąsiedztwa: brak self-loop, brak duplikatów, graf spójny (lub jawnie dopuszczamy “wyspy”)

## STORY 6.2 — MapService: stan mapy i stability tick

* [ ] Inicjalizacja mapy na nowy sezon (neutral owner)
* [ ] `TickStability(now)` — rośnie w czasie, clamp 0..100
* [ ] `ResetMapSeasonIfNeeded(now)` — reset mapy + mapSeasonId (co 7 dni)

**TESTS**
* [ ] Stability rośnie zgodnie z config i nie przekracza 100
* [ ] Nowy sezon: wszystkie sektory neutralne + stability startowe

## STORY 6.3 — MatchmakingService (owner snapshot)

* [ ] Dla neutralnych/bot: bot snapshot w widełkach 80–120% mocy atakującego (config)
* [ ] Dla sektorów z ownerem: użycie `OwnerSnapshot` (MVP)

**TESTS**
* [ ] Bot snapshot: power mieści się w widełkach
* [ ] Owner snapshot: zwraca dokładnie zapisany snapshot

## STORY 6.4 — BattleSimService (strategie + stability)

* [ ] `Simulate(attacker, defender, strategy, stability)` wg `TECH_SPEC.md`
* [ ] Determinizm w testach (seed kontrolowany / wrapper RNG)

**TESTS**
* [ ] StrategyMultiplier wpływa na wynik (statystycznie lub deterministycznie przy seedzie)
* [ ] StabilityMultiplier zwiększa siłę obrony wraz ze stability

## STORY 6.5 — MapService: AttackPreview + AttackSector

* [ ] `GetAttackPreview(sectorId, strategy)` → widełki szansy do UI (przybliżone)
* [ ] `AttackSector(sectorId, strategy)`:
  * walidacja sąsiedztwa (brak teleportu; graf z `MapConfig`)
  * weryfikacja ataków remaining (charge’e) i cooldownu (jeśli włączony)
  * symulacja walki
  * zmiana ownera przy win
  * ustawienie stability po przejęciu wg strategii (niskie/średnie) + timestamp
  * naliczenie nagród soft currency + season points (przez serwisy ekonomii/ligi)
  * zmiana stanu sektorów; anty-snowball (cap/diminishing + maintenance) jest liczony w ekonomii/produkcji (nie w samym ataku)

**TESTS**
* [ ] Atak bez ataków remaining → fail (bez zmian stanu)
* [ ] Atak na niesąsiedni sektor → fail (bez zmian stanu)
* [ ] Win → owner zmieniony, stability ustawione, punkty/nagroda naliczone
* [ ] Lose → owner bez zmian, punkty/nagroda minimalna, cooldown (jeśli włączony)

## STORY 6.6 — Integracja: bonusy sektorów + maintenance → produkcja

* [ ] Produkcja uwzględnia:
  * bonus produkcji z posiadanych sektorów
  * anti-snowball: cap/diminishing returns na sumę bonusów
  * maintenance penalty po przekroczeniu progu sektorów (config)
* [ ] Integracja w `ProductionService.CalculateProductionPerSecond()` (źródło: `MapState` / liczba posiadanych sektorów)

**TESTS**
* [ ] 0 sektorów → brak bonusu i brak maintenance
* [ ] Kilka sektorów → PPS rośnie zgodnie z config
* [ ] Dużo sektorów → diminishing/cap działa + maintenance obniża PPS

---

# EPIC 7 — Ataki (regen) + Rewarded Ads (core)

## STORY 7.1 — Attack charges + regen

* [ ] Serwis w domenie: ataki jako charge’e (0..5) z regenem 1 co 2h (clamp do 5)
* [ ] Integracja z `LastPvpAttackRegenTimestamp` (uwzględnij offline/nieobecność)
* [ ] Premium: możliwość dokupienia dodatkowych ataków (kontrakt + walidacja, bez UI)

**TESTS**
* [ ] Regen przed upływem 2h → brak zmian
* [ ] Regen po 2h → +1 atak (z clampem do 5)
* [ ] Długi offline → regen naliczony poprawnie, ale nie przekracza 5

## STORY 7.2 — AdsService (stub + kontrakt)

* [ ] Interfejs `AdsService.ShowRewardedAd(onSuccess)`
* [ ] Integracja: +1 atak/dzień (rewarded), x2 offline claim (logika domenowa)

**TESTS**
* [ ] “Reward success” zwiększa ataki o 1 (z clampem)
* [ ] Offline claim x2: podwaja nagrodę i loguje event analityczny

---

# EPIC 8 — Subscription (core)

## STORY 8.1 — SubscriptionService (stub)

* [ ] `IsActive` + aplikacja mnożnika produkcji ×2 w kalkulacji (bez UI)
* [ ] Disable interstitial ads (rewarded ads zostają jako opcjonalne placementy)

**TESTS**
* [ ] Włączona subskrypcja → `ProductionPps` ×2
* [ ] Włączona subskrypcja → `PvpPower` bez zmian (bezpośrednio)

---

# EPIC 9 — Analytics (core)

## STORY 9.1 — AnalyticsService wrapper

* [ ] Wrapper `Track(name, params...)` nieblokujący gameplay
* [ ] Eventy z `DESIGN.md`/`TECH_SPEC.md`: `map_open`, `sector_view`, `sector_attack`, `sector_result`, itd.
* [ ] Eventy Overclock: `overclock_activate`, `overclock_charge_gain`, `overclock_charge_spent`

**TESTS**
* [ ] Track nie rzuca wyjątków przy null/empty params
* [ ] Krytyczne ścieżki (offline claim, attack) wywołują Track (przez mock/fake)

---

# EPIC 10 — Bootstrap / Game loop (bez “UI logiki”)

## STORY 10.1 — Bootstrapper

* [ ] Jedno miejsce, które spina:
  * load save
  * init serwisów z configów
  * tick produkcji co 1s
  * tick Overclock (regen/aktywność)
  * tick stability mapy (np. co N sekund / na wejście)
  * autosave
  * aktualizacja `LastLoginTimestamp` (po naliczeniu offline gain)

**TESTS**
* [ ] PlayMode smoke: start gry → brak wyjątków, po kilku tickach stan się zmienia (produkcja rośnie)

## STORY 10.2 — Offline claim (core, bez UI)

Żeby wspierać “x2 offline claim za rewarded”, offline gain nie powinien być automatycznie dodawany bez możliwości decyzji.

* [ ] Serwis domenowy `OfflineClaimService`:
  * na starcie sesji wylicza `PendingOfflineGain` z `LastLoginTimestamp` (cap 12h)
  * `Claim(multiplier)` dodaje CP i czyści pending
  * wspiera `multiplier = 2` po rewarded
* [ ] Telemetria: `offline_claim(amount)` + `ad_watched(placement=offline_x2)`

**TESTS**
* [ ] Offline < cap → pending poprawny
* [ ] Offline > cap → pending policzony jak 12h
* [ ] Claim(1) i Claim(2) dodaje poprawną kwotę i czyści pending

---

# EPIC 11 — UI (na końcu)

Zasada: UI jest cienką warstwą, tylko prezentacja + input → wywołania serwisów.

## STORY 11.0 — Splash screen (UI entry)

* [ ] Splash screen: po starcie/resume (wg ustaleń “single entry”) sprawdza **Update Required** (np. minimalna wersja z Firebase Remote Config) i w razie potrzeby blokuje wejście do gry (CTA do update).

**TESTS**
* [ ] PlayMode: UpdateRequired=true → brak przejścia do Hub
* [ ] PlayMode: UpdateRequired=false → przejście do Hub

## STORY 11.1 — UIRouter + ekrany

* [ ] `UIRouter` i nawigacja: Hub / Generatory / Mapa PvP / Sklep / Event

**TESTS**
* [ ] PlayMode: przełączanie paneli nie gubi stanu, brak crashy

## STORY 11.2 — Hub + Generatory

* [ ] HUD: waluty, production/s, pasek prestiżu
* [ ] Lista generatorów: upgrade x1/x10/max
* [ ] Przycisk **OVERCLOCK AI** (input, bez spamowania)
  * pokazanie charges + cooldown
  * feedback na aktywność (10s) i brak ładunków

**TESTS**
* [ ] PlayMode: klik upgrade → rośnie level i PPS na HUD
* [ ] PlayMode: klik Overclock → PPS wzrasta ×3 na czas aktywności i wraca po 10s

## STORY 11.3 — Prestige modal

* [ ] UI `CanPrestige` + confirm

**TESTS**
* [ ] PlayMode: prestige resetuje UI i stan zgodnie z testami serwisów

## STORY 11.4 — Mapa PvP

* [ ] Mapa 2D + 20–30 sektorów (kolor ownera)
* [ ] Panel sektora: bonus, owner, preview widełek, wybór strategii, atak
* [ ] Tooltip/info w panelu: “Skąd bierze się `PvpPower`?”
  * prestige + permanent upgrades + sektory
  * mały udział z produkcji (soft-cap)
  * subskrypcja: ×2 `ProductionPps`, **nie** zwiększa `PvpPower` bezpośrednio
* [ ] Powiadomienie o wyniku + zmiana koloru sektora

**TESTS**
* [ ] PlayMode: atak sektora zmienia ownera i aktualizuje UI
* [ ] PlayMode: Overclock aktywny przed atakiem wpływa na wynik/preview (w granicach widełek)
* [ ] PlayMode: klik info/tooltip pokazuje i chowa opis `PvpPower` (bez crasha)

## STORY 11.5 — Sklep + Ads + Sub

* [ ] Button rewarded: x2 offline claim, +1 atak/dzień
* [ ] Subskrypcja: stan aktywny + efekt produkcji + wyłączenie interstitial (rewarded zostają)

**TESTS**
* [ ] PlayMode: rewarded success zwiększa ataki i jest widoczne na map screen
* [ ] PlayMode: rewarded +1 Overclock charge jest widoczne na HUD

---

# EPIC 12 — Polishing + Soft Launch Readiness

## STORY 12.1 — Performance + stabilność

* [ ] Profiling low-end (tick + mapa)
* [ ] Memory sanity (brak alokacji per tick)
* [ ] Bugfix pass wg crash logs

**TESTS**
* [ ] PlayMode: 5 minut ticków bez GC spike’ów (monitoring podstawowy)

## STORY 12.2 — Telemetria kompletna

* [ ] Weryfikacja, że wszystkie eventy MUST są emitowane
* [ ] Dashboard sanity (po stronie narzędzia analitycznego)

---

## Minimalna kolejność sprintów (propozycja)

1) EPIC 1–3 (core idle + save)  
2) EPIC 4 (Overclock core)  
3) EPIC 5 (snapshot + liga)  
4) EPIC 7 (ataki/regen + rewarded ads kontrakty)  
5) EPIC 6 (map PvP core; `AttackSector` używa już charge’y)  
6) EPIC 8–9 (sub stub, analytics)  
7) EPIC 10 (bootstrap)  
8) EPIC 11 (UI)  
9) EPIC 12 (polish, readiness)  
