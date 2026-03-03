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

* [x] `SaveService.Load()` (tworzy nowe dane jeśli brak)
* [x] `SaveService.Save()` (atomowy zapis; brak corruptu przy przerwaniu)
* [x] Autosave co 30s + zapis on pause/quit (hook na bootstrapie, nie w UI)
* [x] Wersjonowanie + migracje (stub pod v2, nawet jeśli MVP tylko v1)

**TESTS**
* [x] Save→Load: stan identyczny (w tym sektory mapy)
* [x] Test odporności: uszkodzony plik save → fallback do nowego save (lub backup) bez crasha

---

# EPIC 3 — Ekonomia idle (bez UI)

## STORY 3.1 — BalanceConfig (ScriptableObjects)

* [x] `BalanceConfig` (koszty, produkcje, growth factors, prestige threshold base+growth, permanent upgrade cap, offline cap)
* [x] Wartości przykładowe pod MVP (tylko do testów wewnętrznych)

**TESTS**
* [x] Testy walidacji configu (np. 5 generatorów, brak zer/ujemnych growth factors)

## STORY 3.2 — EconomyService

* [x] `AddCurrency(amount)` i `SpendCurrency(amount)` z walidacją
* [x] Eventy domenowe (np. `CurrencyChanged`) bez zależności od UI

**TESTS**
* [x] Spend > balance → fail (bez zmian stanu)
* [x] Add/Spend z wartościami skrajnymi (0, bardzo małe, bardzo duże)

## STORY 3.3 — ProductionService

* [x] `CalculateProductionPerSecond()`
* [x] Tick co 1s (bez MonoBehaviour w serwisie; tick wywoływany przez bootstrap)
* [x] Offline gain z capem 12h

**TESTS**
* [x] PPS rośnie po upgrade
* [x] Offline gain: cap działa (np. 20h → liczy tylko 12h)
* [x] Determinizm: te same wejścia → te same wyniki

Uwaga: bonusy z sektorów + maintenance (anti-snowball) można spiąć po EPIC 6, żeby nie wprowadzać zależności Map→Core za wcześnie.

## STORY 3.4 — UpgradeService

* [x] Koszt: `baseCost * pow(growthFactor, level)`
* [x] `GetUpgradeCost(generatorId)` + `UpgradeGenerator(generatorId)`
* [x] Obsługa x1/x10/max (logika, bez UI)

**TESTS**
* [x] Koszt rośnie wykładniczo
* [x] Upgrade bez środków → brak zmian
* [x] Upgrade x10 nie przekracza limitów i liczy poprawnie sumaryczny koszt (jeśli implementowane)

## STORY 3.5 — PrestigeService

* [x] `CanPrestige()` (threshold)
* [x] `ExecutePrestige()` reset generatorów, inkrement permanent upgrade, zwiększ multiplier

**TESTS**
* [x] Prestige resetuje tylko to co trzeba (waluty? generatory? zgodnie z designem)
* [x] Permanent upgrade nie przekracza max 10 (clamp)

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

* [x] `OverclockConfig` (czas trwania, cooldown/regen, max charges, mnożniki)

**TESTS**
* [x] Walidacja configu: czasy > 0, mnożniki w sensownych zakresach (guardrails)

## STORY 4.2 — OverclockService

* [x] Stan runtime: ładunki, `ActiveUntil`, cooldown/regen
* [x] `CanActivate(now)` + `Activate(now)`
* [x] `Tick(now)` (regen ładunków, kończenie aktywności)
* [x] Interfejs do innych serwisów:
  * [x] `GetProductionMultiplier(now)` (×3 gdy aktywny)
  * [x] `GetPvpAttackMultiplier(now)` (+20% gdy aktywny)
  * [x] `GetFreshCaptureStabilityGrowthMultiplier(now)` (+10% gdy aktywny)

**TESTS**
* [x] Activate zużywa ładunek i ustawia `ActiveUntil`
* [x] Brak ładunków → nie da się aktywować
* [x] Regen: 1 ładunek co 90s, clamp do max 2
* [x] Cooldown/regen nie “stackuje się” i nie daje > max ładunków
* [x] Multipliery działają tylko w oknie aktywności

## STORY 4.3 — Integracje (bez UI)

* [x] `ProductionService` uwzględnia multiplier z Overclock (w kalkulacji PPS i ticku)
* [x] `BattleSimService` uwzględnia bonus attack power z Overclock (na atakującym)
* [x] `MapService.TickStability` uwzględnia +10% wzrostu stability dla **świeżo zdobytych** sektorów, gdy Overclock aktywny
  * “Świeżo zdobyty” = sektor przejęty w ostatnich `FreshCaptureWindowSeconds` (config)
* [x] Telemetria: `overclock_activate`, `overclock_charge_spent`, `overclock_charge_gain` (domenowe eventy; mapowanie do analytics w EPIC 9)

**TESTS**
* [x] Włączony Overclock → PPS rośnie ×3
* [x] Włączony Overclock → wyższa szansa wygranej w identycznych warunkach (deterministyczny seed)
* [x] Włączony Overclock → stability świeżo przejętego sektora rośnie szybciej (przy tym samym czasie)

---

# EPIC 5 — PvP: Snapshot + Ligi (bez mapy jeszcze)

## STORY 5.1 — SnapshotService

* [x] Budowa `PvpSnapshot` z aktualnego `GameState`
* [x] `PvpPower` liczony z osobnej statystyki (sub ×2 nie działa bezpośrednio)
  * core progression: prestige, permanent upgrades, sektory
  * + mały udział z produkcji przez soft-cap (z `BaseProductionPpsWithoutSubscription`)
* [x] Konfiguracja w `PvpConfig`:
  * `PrestigePvpPowerPerPrestige`, `PermanentPvpPowerPerLevel`, `SectorPvpPowerPerSector`
  * `ProdToPvpMaxBonus`, `ProdToPvpHalfCapPps`

**TESTS**
* [x] Snapshot `PvpPower` nie zmienia się po włączeniu subskrypcji (bezpośrednio) *(MVP: dowód przez użycie `BaseProductionPpsWithoutSubscription`; test pokryty przez brak wpływu tymczasowego boosta produkcji / Overclock)*
* [x] Snapshot `PvpPower` rośnie po prestiżu / permanent upgrade / sektorach

## STORY 5.2 — LeagueService (Season Points)

* [x] Progi lig w `LeagueConfig`
* [x] Dodawanie punktów sezonu + awans/spadek
* [x] Reset sezonu ligi (30 dni; date-based / `leagueSeasonId`) — w logice domenowej, bez UI *(MVP: sezon jako miesiąc kalendarzowy UTC; możliwy tryb fixed-days do przyszłej rekonfiguracji)*

**TESTS**
* [x] Punkty → prawidłowa liga (progi)
* [x] Reset sezonu ligi zeruje punkty i odświeża `leagueSeasonId`

## STORY 5.3 — PvpConfig (walidacja i guardrails)

* [x] Walidacja `PvpConfig` (ScriptableObject) na starcie gry / w testach:
  * `PowerVarianceMin > 0` i `PowerVarianceMin <= PowerVarianceMax`
  * `StrategyMultiplier* > 0`
  * `StabilityMultiplierMin > 0` i `StabilityMultiplierMin <= StabilityMultiplierMax`
  * `PrestigePvpPowerPerPrestige >= 0`, `PermanentPvpPowerPerLevel >= 0`, `SectorPvpPowerPerSector >= 0`
  * `ProdToPvpMaxBonus >= 0` oraz `ProdToPvpMaxBonus <= 1` (max +100% do core)
  * `ProdToPvpHalfCapPps > 0`

**TESTS**
* [x] Nieprawidłowy config → błąd walidacji (fail fast) lub bezpieczny fallback (zależnie od decyzji)
* [x] Poprawny config → przechodzi walidację

---

# EPIC 6 — PvP: Mapa sektorów (core)

## STORY 6.1 — MapConfig

* [x] `MapConfig` (reset mapy co 7 dni, lista sektorów + sąsiedztwo, bonusy produkcji + bonusy PvP, cap/diminishing returns, stability parametry, cooldown)
* [x] `FreshCaptureWindowSeconds` (okno “świeżo zdobytego” sektora pod bonus Overclock)

**TESTS**
* [x] Walidacja: 20–30 sektorów, unikalne `SectorId`, bonusy w sensownym zakresie
* [x] Walidacja sąsiedztwa: brak self-loop, brak duplikatów, graf spójny (lub jawnie dopuszczamy “wyspy”)

## STORY 6.2 — MapService: stan mapy i stability tick

* [x] Inicjalizacja mapy na nowy sezon (neutral owner + home sector local playera)
* [x] `TickStability(now)` — rośnie w czasie, clamp 0..100
* [x] `ResetMapSeasonIfNeeded(now)` — reset mapy + mapSeasonId (co 7 dni)

**TESTS**
* [x] Stability rośnie zgodnie z config i nie przekracza 100
* [x] Nowy sezon: wszystkie sektory neutralne + stability startowe (+ home sector zostaje owned)

## STORY 6.3 — MatchmakingService (owner snapshot)

* [x] Dla neutralnych/bot: bot snapshot w widełkach 80–120% mocy atakującego (config)
* [x] Dla sektorów z ownerem: użycie `OwnerSnapshot` (MVP)

**TESTS**
* [x] Bot snapshot: power mieści się w widełkach
* [x] Owner snapshot: zwraca dokładnie zapisany snapshot

## STORY 6.4 — BattleSimService (strategie + stability)

* [x] `Simulate(attacker, defender, strategy, stability)` wg `TECH_SPEC.md`
* [x] Determinizm w testach (seed kontrolowany / wrapper RNG)

**TESTS**
* [x] StrategyMultiplier wpływa na wynik (statystycznie lub deterministycznie przy seedzie)
* [x] StabilityMultiplier zwiększa siłę obrony wraz ze stability

## STORY 6.5 — MapService: AttackPreview + AttackSector

* [x] `GetAttackPreview(sectorId, strategy)` → widełki szansy do UI (przybliżone)
* [x] `AttackSector(sectorId, strategy)`:
  * walidacja sąsiedztwa (brak teleportu; graf z `MapConfig`)
  * weryfikacja ataków remaining (charge’e) i cooldownu (jeśli włączony)
  * symulacja walki
  * zmiana ownera przy win
  * ustawienie stability po przejęciu wg strategii (niskie/średnie) + timestamp
  * naliczenie nagród soft currency + season points (przez serwisy ekonomii/ligi)
  * zmiana stanu sektorów; anty-snowball (cap/diminishing + maintenance) jest liczony w ekonomii/produkcji (nie w samym ataku)

**TESTS**
* [x] Atak bez ataków remaining → fail (bez zmian stanu)
* [x] Atak na niesąsiedni sektor → fail (bez zmian stanu)
* [x] Win → owner zmieniony, stability ustawione, punkty/nagroda naliczone
* [x] Lose → owner bez zmian, punkty/nagroda minimalna, cooldown (jeśli włączony)

## STORY 6.6 — Integracja: bonusy sektorów + maintenance → produkcja

* [x] Produkcja uwzględnia:
  * bonus produkcji z posiadanych sektorów
  * anti-snowball: cap/diminishing returns na sumę bonusów
  * maintenance penalty po przekroczeniu progu sektorów (config)
* [x] Integracja w `ProductionService.CalculateProductionPerSecond()` (źródło: `MapState` / liczba posiadanych sektorów)

**TESTS**
* [x] 0 sektorów → brak bonusu i brak maintenance
* [x] Kilka sektorów → PPS rośnie zgodnie z config
* [x] Dużo sektorów → diminishing/cap działa + maintenance obniża PPS

---

# EPIC 7 — Ataki (regen) + Rewarded Ads (core)

## STORY 7.1 — Attack charges + regen

* [x] Serwis w domenie: ataki jako charge’e (0..5) z regenem 1 co 2h (clamp do 5)
* [x] Integracja z `LastPvpAttackRegenTimestamp` (uwzględnij offline/nieobecność)
* [x] Premium: możliwość dokupienia dodatkowych ataków (kontrakt + walidacja, bez UI)

**TESTS**
* [x] Regen przed upływem 2h → brak zmian
* [x] Regen po 2h → +1 atak (z clampem do 5)
* [x] Długi offline → regen naliczony poprawnie, ale nie przekracza 5

## STORY 7.2 — AdsService (stub + kontrakt)

* [x] Interfejs `AdsService.ShowRewardedAd(onSuccess)`
* [x] Integracja: +1 atak/dzień (rewarded), x2 offline claim (logika domenowa)

**TESTS**
* [x] “Reward success” zwiększa ataki o 1 (z clampem)
* [x] Offline claim x2: podwaja nagrodę i loguje event analityczny

---

# EPIC 8 — Subscription (core)

## STORY 8.1 — SubscriptionService (stub)

* [x] `IsActive` + aplikacja mnożnika produkcji ×2 w kalkulacji (bez UI)
* [x] Disable interstitial ads (rewarded ads zostają jako opcjonalne placementy)

**TESTS**
* [x] Włączona subskrypcja → `ProductionPps` ×2
* [x] Włączona subskrypcja → `PvpPower` bez zmian (bezpośrednio)

---

# EPIC 9 — Analytics (core)

## STORY 9.1 — AnalyticsService wrapper

* [x] Wrapper `Track(name, params...)` nieblokujący gameplay
* [x] Eventy z `DESIGN.md`/`TECH_SPEC.md`: `map_open`, `sector_view`, `sector_attack`, `sector_result`, itd.
* [x] Eventy Overclock: `overclock_activate`, `overclock_charge_gain`, `overclock_charge_spent`

**TESTS**
* [x] Track nie rzuca wyjątków przy null/empty params
* [x] Krytyczne ścieżki (offline claim, attack) wywołują Track (przez mock/fake)

---

# EPIC 10 — Bootstrap / Game loop (bez “UI logiki”)

## STORY 10.1 — Bootstrapper

* [x] Jedno miejsce, które spina:
  * load save
  * init serwisów z configów
  * tick produkcji co 1s
  * tick Overclock (regen/aktywność)
  * tick stability mapy (np. co N sekund / na wejście)
  * autosave
  * aktualizacja `LastLoginTimestamp` (po naliczeniu offline gain)

**TESTS**
* [x] PlayMode smoke: start gry → brak wyjątków, po kilku tickach stan się zmienia (produkcja rośnie)

## STORY 10.2 — Offline claim (core, bez UI)

Żeby wspierać “x2 offline claim za rewarded”, offline gain nie powinien być automatycznie dodawany bez możliwości decyzji.

* [x] Serwis domenowy `OfflineClaimService`:
  * na starcie sesji bankuje `PendingOfflineGain` z `LastLoginTimestamp` (cap 12h) i zapisuje pending w save (crash-safe)
  * `Claim(multiplier)` dodaje CP i czyści pending
  * wspiera `multiplier = 2` po rewarded
* [x] Telemetria: `offline_claim(amount)` + `ad_watched(placement=offline_x2)`

**TESTS**
* [x] Offline < cap → pending poprawny
* [x] Offline > cap → pending policzony jak 12h
* [x] Claim(1) i Claim(2) dodaje poprawną kwotę i czyści pending

---

# EPIC 11 — UI (na końcu)

Zasada: UI jest cienką warstwą, tylko prezentacja + input → wywołania serwisów.

## STORY 11.0 — Splash screen (UI entry)

* [ ] Splash screen: po starcie/resume (wg ustaleń “single entry”) sprawdza **Update Required** (np. minimalna wersja z Firebase Remote Config) i w razie potrzeby blokuje wejście do gry (CTA do update).

**TESTS**
* [ ] PlayMode: UpdateRequired=true → brak przejścia do Hub
* [ ] PlayMode: UpdateRequired=false → przejście do Hub

## STORY 11.0b — Resume policy (10 min → Splash)

* [ ] Jeśli app wraca z tła po **>= 10 minutach** → pokazujemy Splash (jedyny entry) i dopiero potem routing do Hub.
* [ ] Jeśli app wraca szybciej → bez Splash, zostajemy w bieżącym ekranie/stanie.

**TESTS**
* [ ] PlayMode: resume po >= 10 min → przejście przez Splash → Hub
* [ ] PlayMode: resume po < 10 min → brak Splash, brak resetu routingu

## STORY 11.1 — UIRouter + ekrany

* [ ] `UIRouter` i nawigacja: Hub / Generatory / Mapa PvP / Sklep / Event

**TESTS**
* [ ] PlayMode: przełączanie paneli nie gubi stanu, brak crashy

## STORY 11.1b — Stany UI + feedback (fail-safe)

* [ ] Wszystkie akcje UI mają bezpieczne guardy + feedback:
  * przyciski disabled gdy akcja niedozwolona (brak waluty, brak charge’y, cooldown, brak adjacency)
  * toast/tooltip z powodem (np. “Brak ataków”, “Cooldown”, “Nie sąsiaduje z Twoim sektorem”)
  * brak wyjątków / brak spamowania klikami (debounce/throttle na krytycznych akcjach)

**TESTS**
* [ ] PlayMode: klik “zablokowanej” akcji nie zmienia stanu i pokazuje powód
* [ ] PlayMode: spam tap (np. 20 klików/sek) nie crashuje i nie powoduje wielokrotnego wywołania use-case’u

## STORY 11.2 — Hub + Generatory

* [ ] HUD: waluty, production/s, pasek prestiżu
* [ ] Lista generatorów: upgrade x1/x10/max
* [ ] Przycisk **OVERCLOCK AI** (input, bez spamowania)
  * pokazanie charges + cooldown
  * feedback na aktywność (10s) i brak ładunków

**TESTS**
* [ ] PlayMode: klik upgrade → rośnie level i PPS na HUD
* [ ] PlayMode: klik Overclock → PPS wzrasta ×3 na czas aktywności i wraca po 10s

## STORY 11.2b — Offline claim modal (pending gain)

* [ ] Na wejściu do Hub (po starcie/resume przez Splash): jeśli `PendingOfflineGain > 0` → modal:
  * pokazuje kwotę + wyjaśnienie skąd (czas offline, cap 12h)
  * `Claim x1` → `OfflineClaimService.Claim(1)`
  * `Claim x2` (rewarded) → pokazuje rewarded (`offline_x2`) i po sukcesie `OfflineClaimService.Claim(2)`
  * “Not now” → zamyka modal bez claim (pending zostaje, crash-safe)
* [ ] Po claim: HUD odświeżony (waluta + lifetime), modal znika

**TESTS**
* [ ] PlayMode: PendingOfflineGain>0 → modal się pokazuje
* [ ] PlayMode: Claim x1 czyści pending i dodaje poprawną walutę
* [ ] PlayMode: rewarded success → Claim x2 czyści pending i dodaje ×2
* [ ] PlayMode: “Not now” → brak zmian stanu, pending zostaje

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

## STORY 11.4b — PvP HUD + statusy (ataki/regen/season)

* [ ] Na ekranie mapy:
  * licznik `PvPAttacksRemaining` + timer do kolejnego regen (jeśli < cap)
  * aktualna liga + `SeasonPoints`
  * stan sezonu (np. “Season reset soon” / id) lub chociaż informacja o resetach (żeby uniknąć “why it reset?”)
* [ ] Panel sektora pokazuje (oprócz istniejących wymagań):
  * `Stability` + tempo zmian (jeśli jest) lub przynajmniej wartość + “meaning”
  * cooldown do kolejnego ataku (jeśli aktywny)
  * powód braku możliwości ataku (brak adjacency, home sector, sektor już jest Twój, brak ataków, cooldown, season reset)

**TESTS**
* [ ] PlayMode: po ataku zmienia się `PvPAttacksRemaining` i HUD się odświeża
* [ ] PlayMode: regen ataków aktualizuje HUD po upływie czasu
* [ ] PlayMode: cooldown sektora blokuje atak i UI pokazuje powód

## STORY 11.5 — Sklep + Ads + Sub

* [ ] Button rewarded: x2 offline claim, +1 atak/dzień
* [ ] Subskrypcja: stan aktywny + efekt produkcji + wyłączenie interstitial (rewarded zostają)

**TESTS**
* [ ] PlayMode: rewarded success zwiększa ataki i jest widoczne na map screen
* [ ] PlayMode: rewarded +1 Overclock charge jest widoczne na HUD

## STORY 11.5b — PremiumCurrency (MVP: prezentacja + placeholder)

* [ ] HUD pokazuje `PremiumCurrency` (jeśli jest w `GameState`)
* [ ] Jeśli w MVP nie dowozimy zakupów premium: przyciski są placeholder (disabled) + opis “coming later” (bez błędów)
* [ ] Jeśli dowozimy minimalny flow: osobny stub “grant premium” tylko dla dev build (żeby testować UI zależne od premium)

**TESTS**
* [ ] PlayMode: PremiumCurrency jest poprawnie wyświetlane i nie powoduje crashy przy zmianie wartości

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
