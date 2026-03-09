# AI Wars Idle (MVP v1)

Repo/notes + projekt Unity: `aiwarsidle/` (Unity 6.3, Android-only, uGUI).

## Progress

### EPIC 0 — Repo + Fundamenty projektu (DONE)

- Dodana struktura folderów w `aiwarsidle/Assets/` wg `TECH_SPEC.md` (`GameCore`, `PvP`, `Persistence`, `Data`, `Analytics`, `Monetization`, `UI`, `Tests`).
- Warstwy rozdzielone przez asmdef: `aiwarsidle/Assets/*/*.asmdef` + osobne test assemblies dla EditMode/PlayMode.
- Smoke testy Unity Test Framework:
  - EditMode: `aiwarsidle/Assets/Tests/EditMode/SmokeTests_EditMode.cs`
  - PlayMode: `aiwarsidle/Assets/Tests/PlayMode/SmokeTests_PlayMode.cs`
- Git hygiene: `.gitignore` ignoruje `Library/`, `Temp/`, buildy oraz pliki IDE/solutions.

Źródło planu i dalszych kroków: `TASKS_delivery_core_first.md`.

### EPIC 1 — Modele domenowe + kontrakty (DONE)

- Dodane modele domenowe (bez logiki) w `aiwarsidle/Assets/GameCore/Domain/` (m.in. `GameState`, `PvpSnapshot`, modele mapy PvP, `OverclockState`).
- Dodana walidacja zakresów (stability 0..100, timestampy >= 0, charges 0..max) w `aiwarsidle/Assets/GameCore/Validation/DomainValidation.cs`.
- Testy EditMode: walidacje + JSON roundtrip `GameState` w `aiwarsidle/Assets/Tests/EditMode/`.
- Dodana paczka `com.unity.nuget.newtonsoft-json` (używana w testach i później pod save/load).

### EPIC 2 — Persistence (Save/Load) + migracje

#### STORY 2.1 — SaveData schema v1 (DONE)

- Dodany schema model `SaveDataV1` + `SectorSaveData` + `OverclockSaveData` w `aiwarsidle/Assets/Persistence/Domain/SaveDataV1.cs`.
- Dodana normalizacja/clamp danych (`Normalize()`): brak nulli, `Stability` 0..100, timestampy >= 0, Overclock charges 0..2.
- Testy EditMode schemy w `aiwarsidle/Assets/Tests/EditMode/SaveDataV1SchemaTests.cs`.

#### STORY 2.2 — SaveService (DONE)

- `SaveService` z `LoadOrCreate()` i atomowym zapisem + backup w `aiwarsidle/Assets/Persistence/Services/SaveService.cs`.
- Wersjonowanie/migracje: `SaveDataMigrator` (stub pod kolejne wersje) w `aiwarsidle/Assets/Persistence/Services/SaveDataMigrator.cs`.
- Autosave: `AutosaveRunner` (tick + save on pause/quit; do podpięcia w bootstrapperze) w `aiwarsidle/Assets/Persistence/Services/AutosaveRunner.cs`.
- Testy EditMode: Save→Load roundtrip + corrupt save fallback w `aiwarsidle/Assets/Tests/EditMode/SaveServiceTests.cs`.

### EPIC 3 — Ekonomia idle (bez UI) (DONE)

- Dodany `BalanceConfig` (ScriptableObject) + walidacja runtime fail-fast w `aiwarsidle/Assets/GameCore/Config/BalanceConfig.cs`.
- Ekonomia oparta o **lifetime CP**:
  - `GameState.LifetimeEarnedSoftCurrency` w `aiwarsidle/Assets/GameCore/Domain/GameState.cs`.
  - `SaveDataV1.LifetimeEarnedSoftCurrency` + `Normalize()` w `aiwarsidle/Assets/Persistence/Domain/SaveDataV1.cs`.
- `GlobalMultiplier` jest **wyliczany**, nie trzymany w stanie (helper `PrestigeMath`) w `aiwarsidle/Assets/GameCore/Services/PrestigeMath.cs`.
- Serwisy core (bez MonoBehaviour / bez UI):
  - `EconomyService` (`AddCurrency`/`SpendCurrency`) + domenowy event `CurrencyChangedEvent` w `aiwarsidle/Assets/GameCore/Services/EconomyService.cs`.
  - `ProductionService` (PPS, tick, offline cap 12h) w `aiwarsidle/Assets/GameCore/Services/ProductionService.cs`.
  - `UpgradeService` (koszt `baseCost * pow(growthFactor, level)` + x1/x10/max) w `aiwarsidle/Assets/GameCore/Services/UpgradeService.cs`.
  - `PrestigeService` (próg od lifetime CP, reset generatorów, permanent cap) w `aiwarsidle/Assets/GameCore/Services/PrestigeService.cs`.
  - `OfflineClaimService` (pending offline gain + `Claim(multiplier)` pod przyszłe rewarded x2; claim nabija też lifetime) w `aiwarsidle/Assets/GameCore/Services/OfflineClaimService.cs`.
- Lekki `EventBus` (core→UI/telemetria) w `aiwarsidle/Assets/GameCore/Services/EventBus.cs`.
- Testy EditMode pokrywające EPIC 3 dodane/rozszerzone w `aiwarsidle/Assets/Tests/EditMode/DomainModelValidationTests.cs`.

## Notatka (lokalne buildy bez sieci)

- Repo zawiera `aiwarsidle/NuGet.Config`, który czyści `packageSources`, żeby `dotnet restore/build` działało w środowiskach bez dostępu do `nuget.org`.

---

## EPIC 4 — Overclock (core) (DONE)

- Dodany `OverclockConfig` (ScriptableObject) + walidacja runtime (`ValidateOrThrow`) w `aiwarsidle/Assets/GameCore/Config/OverclockConfig.cs`.
- Dodany `OverclockService` (ładunki, aktywność, regen, multipliery) w `aiwarsidle/Assets/GameCore/Services/OverclockService.cs`.
- Dodane eventy domenowe pod telemetrię: `OverclockActivatedEvent`, `OverclockChargeSpentEvent`, `OverclockChargeGainedEvent` w `aiwarsidle/Assets/GameCore/Services/OverclockEvents.cs` (mapowanie na eventy analytics w EPIC 9).
- Integracja ekonomii: `ProductionService` uwzględnia mnożnik produkcji z Overclock przy podaniu `nowUnixSeconds` w `aiwarsidle/Assets/GameCore/Services/ProductionService.cs`.
- Integracje PvP/Mapa (bez UI): minimalne `BattleSimService` i `MapService` uwzględniają boosty Overclock (atak i wzrost stability świeżo zdobytego sektora) w `aiwarsidle/Assets/PvP/`.
- Testy EditMode dla EPIC 4 w `aiwarsidle/Assets/Tests/EditMode/OverclockEpic4Tests.cs`.
- Save: usunięty twardy clamp Overclock do 2 ładunków w `aiwarsidle/Assets/Persistence/Domain/SaveDataV1.cs` (max ładunków jest konfigurowalny przez `OverclockConfig`).

---

## EPIC 5 — PvP: Snapshot + Ligi (bez mapy jeszcze) (DONE)

- Snapshot PvP:
  - `SnapshotService` buduje `PvpSnapshot` oraz liczy `PvpPower` z core progression (prestige/permanent/sektory) + soft-cap bonus z `BaseProductionPerSecondWithoutSubscription` w `aiwarsidle/Assets/PvP/Services/SnapshotService.cs`.
  - `PvpConfig` rozszerzony o parametry do liczenia `PvpPower` + walidacje guardrails (w tym `ProdToPvpMaxBonus` 0..1, `ProdToPvpHalfCapPps > 0`) w `aiwarsidle/Assets/PvP/Config/PvpConfig.cs`.
- Liga / Season Points:
  - `LeagueConfig` z progami lig jako tablica (łatwe do zmiany) + tryb sezonu `CalendarMonthUtc` lub `FixedDays` w `aiwarsidle/Assets/PvP/Config/LeagueConfig.cs`.
  - `LeagueService` (punkty sezonu, awans/spadek, reset sezonu przez `leagueSeasonId`) w `aiwarsidle/Assets/PvP/Services/LeagueService.cs`.
  - W configu i kodzie jest wzmianka o bezpiecznym rollout’cie zmian schematu sezonu (“apply from next season”), żeby uniknąć resetów w środku sezonu.
- Testy EditMode dla EPIC 5 w `aiwarsidle/Assets/Tests/EditMode/Epic5SnapshotLeagueTests.cs`.

---

## EPIC 6 — PvP: Mapa sektorów (core) (DONE)

Aktualne założenie dokumentacyjne MVP: mapa PvP ma być docelowo rozgrywana jako **PvE (gracz vs 5 botów)** na heksagonalnej siatce (`radius = 4`, ok. 61 sektorów), z jednym nietykalnym **Home Sector** na stronę.

- `MapConfig` (ScriptableObject) z:
  - sezonem mapy (czas resetu konfigurowalny; obecny target produktowy: ~72h), definicjami sektorów + adjacency, stability/capture/cooldown
  - bonusami produkcji (cap/diminishing + maintenance) oraz bonusami PvP per-sektor
  - walidacjami (unikalne `SectorId`, brak self-loop/duplikatów, opcjonalnie graf spójny)
  w `aiwarsidle/Assets/PvP/Config/MapConfig.cs`.
- `MapService`:
  - `ResetMapSeasonIfNeeded(now)` i `TickStability(now)` (clamp 0..100)
  - jawny “single entry” do update’u mapy: `AdvanceTime(now)` (init/reset sezonu + tick stability)
  w `aiwarsidle/Assets/PvP/Services/MapService.cs`.
- Matchmaking obrońcy (MVP):
  - bot snapshot w widełkach (config) lub użycie `OwnerSnapshot` jeśli sektor ma ownera
  w `aiwarsidle/Assets/PvP/Services/MatchmakingService.cs`.
  - neutral sectors mają teraz dodatkowy easing early-game zależny od dystansu po grafie od **home atakującej frakcji**:
    - dla neutrali liczone jest BFS-em, ile kroków w `Adjacency` dzieli cel od home danego atakującego
    - im bliżej własnego home, tym niższy efektywny `NeutralPowerMinMultiplier` / `NeutralPowerMaxMultiplier`
    - easing nie liczy dystansu od „dowolnego home”, tylko od home przypisanego do konkretnego gracza/bota wykonującego atak
    - tuning przez `MapConfig.NeutralPowerHomeDistanceMinFactor` i `MapConfig.NeutralPowerHomeDistanceStepFactor`
  - efekt produktowy: pierwszy ring neutralnych sektorów wokół home jest łatwiejszy do zajęcia, co przyspiesza opening bez spłaszczania środka mapy
  w `aiwarsidle/Assets/PvP/Services/MatchmakingService.cs` i `aiwarsidle/Assets/PvP/Config/MapConfig.cs`.

Uwaga: dokumentacja produktowa została zaktualizowana do docelowego układu hex-grid / 6 stron / ~72h; kod runtime nie został jeszcze w tym commicie przepięty do tych nowych założeń.
- PvP na mapie:
  - `GetAttackPreview` (widełki szansy) oraz `AttackSector` (walidacje adjacency/cooldown/charge’y, symulacja, update sektora, reward + season points)
  w `aiwarsidle/Assets/PvP/Services/PvpMapCombatService.cs`.
- Produkcja a sektory (anti-snowball):
  - `ProductionService` dostał hook `IPermanentProductionMultiplierProvider` na stałe mnożniki (bez zależności GameCore→PvP)
  w `aiwarsidle/Assets/GameCore/Services/ProductionService.cs`.
  - Implementacja mnożnika z mapy: `MapProductionBonusProvider` w `aiwarsidle/Assets/PvP/Services/MapProductionBonusProvider.cs`.

Testy EditMode dla EPIC 6: `aiwarsidle/Assets/Tests/EditMode/Epic6MapCoreTests.cs`.

---

## EPIC 7 — Ataki (regen) + Rewarded Ads (core) (DONE)

- PvP attack charges (regen, offline, clamp do capu, bez bankowania czasu na capie):
  - Balans: `PvpAttacksConfig` w `aiwarsidle/Assets/PvP/Config/PvpAttacksConfig.cs`.
  - Logika domenowa: `PvpAttackChargesService` w `aiwarsidle/Assets/PvP/Services/PvpAttackChargesService.cs`.
  - Stan: dodane `NextPvpAttackRegenAtUnixSeconds` do `GameState` + save schema w `aiwarsidle/Assets/Persistence/Domain/SaveDataV1.cs`.
  - Integracja w PvP: `PvpMapCombatService` zużywa ataki przez serwis (a nie przez bezpośrednią mutację stanu).
- Rewarded ads (core kontrakty + use-case’y):
  - Kontrakt `IAdsService.ShowRewardedAd(Action onSuccess)` w `aiwarsidle/Assets/GameCore/Services/IAdsService.cs` + stub `AdsService` w `aiwarsidle/Assets/Monetization/AdsService.cs`.
  - Kontrakt analytics: `IAnalyticsService.Track(...)` w `aiwarsidle/Assets/GameCore/Services/IAnalyticsService.cs` (fake w testach).
  - Use-case serwis: `RewardedAdsUseCaseService` (`offline_x2`, `pvp_attack_daily`) w `aiwarsidle/Assets/PvP/Services/RewardedAdsUseCaseService.cs`.
  - Daily claim liczone po **UTC day boundary**; `LastPvpAdAttackClaimUnixSeconds = 0` oznacza “nigdy nie claimowano”.
- Testy EditMode:
  - EPIC 7 charges/regen: `aiwarsidle/Assets/Tests/EditMode/Epic7PvpAttackChargesTests.cs`.
  - EPIC 7 rewarded use-case: `aiwarsidle/Assets/Tests/EditMode/Epic7RewardedAdsUseCaseTests.cs`.

---

## EPIC 8 — Subscription (core) (DONE)

- Subskrypcja (stub, bez UI / bez weryfikacji store):
  - Kontrakt: `ISubscriptionService.IsActive` w `aiwarsidle/Assets/GameCore/Services/ISubscriptionService.cs`.
  - Stub implementacji: `SubscriptionService` w `aiwarsidle/Assets/Monetization/SubscriptionService.cs`.
- Produkcja:
  - `ProductionService` mnoży `ProductionPps` ×2 gdy subskrypcja aktywna, ale zachowuje osobną metodę `CalculateBaseProductionPerSecondWithoutSubscription()` pod PvP w `aiwarsidle/Assets/GameCore/Services/ProductionService.cs`.
- Reklamy:
  - Rewarded ads pozostają bez zmian (`IAdsService`).
  - Dodany kontrakt interstitial + stub, który nie pokazuje interstitiali przy aktywnej subskrypcji: `IInterstitialAdsService` w `aiwarsidle/Assets/GameCore/Services/IInterstitialAdsService.cs` + `InterstitialAdsService` w `aiwarsidle/Assets/Monetization/InterstitialAdsService.cs`.
- Testy EditMode EPIC 8: `aiwarsidle/Assets/Tests/EditMode/Epic8SubscriptionTests.cs`.

---

## EPIC 9 — Analytics (core) (DONE)

- Dodany nieblokujący gameplay `IAnalyticsService` jako bufor:
  - `BufferedAnalyticsService` + `IAnalyticsSink` (pod runtime sink Firebase/Unity Analytics itp.)
  w `aiwarsidle/Assets/Analytics/BufferedAnalyticsService.cs` oraz `aiwarsidle/Assets/Analytics/IAnalyticsSink.cs`.
- EventBus → Analytics:
  - `AnalyticsEventBusBridge` mapuje domenowe eventy na nazwy z `DESIGN.md`/`TECH_SPEC.md`
    (m.in. `session_start`, `offline_claim`, `generator_upgrade`, `prestige`, `map_open`, `sector_view`, `sector_attack`, `sector_result`,
    `sector_ownership_changed`, `ad_watched`, `subscription_started`, plus Overclock).
  w `aiwarsidle/Assets/Analytics/AnalyticsEventBusBridge.cs`.
- Publikacja eventów domenowych (core):
  - offline claim / upgrade / prestige / subscription / rewarded / PvP attack/result/ownership
  w `aiwarsidle/Assets/GameCore/Services/*` oraz `aiwarsidle/Assets/PvP/Services/*`.
- Telemetria bez UI:
  - `SessionTelemetryService` (`session_start`) i `PvpTelemetryService` (`map_open`, `sector_view`)
  w `aiwarsidle/Assets/GameCore/Services/SessionTelemetryService.cs` i `aiwarsidle/Assets/PvP/Services/PvpTelemetryService.cs`.
- Testy EditMode EPIC 9: `aiwarsidle/Assets/Tests/EditMode/Epic9AnalyticsTests.cs` (w tym null/empty params + krytyczne ścieżki).

Uwaga: `Track(...)` tylko buforuje — w runtime trzeba wołać `Flush()` cyklicznie (np. w bootstrapie).

---

## EPIC 10 — Bootstrap / Game loop (bez UI logiki) (DONE)

- Dodany composition root + game loop:
  - `GameBootstrapper` (Unity lifecycle: `Update`, pause/quit) w `aiwarsidle/Assets/Bootstrap/GameBootstrapper.cs`.
  - `GameLoop` (init serwisów + tick: produkcja/overclock/mapa + autosave + analytics flush) w `aiwarsidle/Assets/Bootstrap/GameLoop.cs`.
  - Osobny asmdef `AIWarsIdle.Bootstrap` w `aiwarsidle/Assets/Bootstrap/AIWarsIdle.Bootstrap.asmdef`.
- Save/load runtime:
  - Mapper `SaveDataV1` ↔ `GameState` w `aiwarsidle/Assets/Persistence/Services/SaveDataV1GameStateMapper.cs`.
- Offline claim “crash-safe”:
  - `PendingOfflineGain` jest persystowane w save (`SaveDataV1`) oraz w runtime (`GameState`), żeby restart przed UI-claim nie gubił pending.
  - `OfflineClaimService.BankOfflineGain(now)` bankuje offline gain i aktualizuje `LastLoginUnixSeconds` w sposób odporny na crash.
- Testy:
  - PlayMode smoke bootstrap/tick: `aiwarsidle/Assets/Tests/PlayMode/Epic10BootstrapPlayModeTests.cs`.
  - EditMode testy offline claim z checklisty EPIC10: `aiwarsidle/Assets/Tests/EditMode/Epic10OfflineClaimServiceTests.cs`.

---

## EPIC 11 — UI (WIP)

Minimalne fundamenty UI pod Splash/Hub oraz tematyzację:

- Scena pod layout:
  - Dodany `Canvas` + `LayoutRoot` w `aiwarsidle/Assets/Scenes/SampleScene.unity` (do szybkiego ustawiania uGUI layoutów).
- Theme (kolory spójne w prefabach):
  - `UITheme` (ScriptableObject) + `UIThemeProvider` + `UIThemeBinder` w `aiwarsidle/Assets/UI/Common/Theme/`.
- Splash / single-entry + resume policy (>= 10 min → Splash):
  - `ResumePolicyController` w `aiwarsidle/Assets/UI/Splash/ResumePolicyController.cs`:
    - tryb “touch to continue” (aktywny `touch_continue_dim`) po timeout resuma
    - force update: aktywuje `force_update` i blokuje wejście do `hub_root`, gdy `min_app_version` (Firebase Remote Config) > app build number
    - `download_button` (pod `force_update/force_update_pop/download_button`) otwiera URL z Remote Config `store_url`
  - `SplashVersionLabel` w `aiwarsidle/Assets/UI/Splash/SplashVersionLabel.cs` pokazuje `Application.version` + build number w formacie `0.0.0 (X)`.
  - Dev debug button do triggerowania timeoutu: `aiwarsidle/Assets/UI/Splash/ResumePolicyDebugButton.cs`.

UIRouter + ekrany (STORY 11.1, etap: 2 taby — PvP + Generatory):

- `UIRouter` w `aiwarsidle/Assets/UI/UIRouter.cs`:
  - przełącza tylko `pvp_root` i `generators_root` (pozostałe rooty później),
  - `LobbyRoot` (HUD/Lobby) zostaje aktywny cały czas,
  - taby mają stan aktywności (blokuje klik na aktywnym tabie) + opcjonalne przygaszanie alpha na `Icon` (`Graphic[]`) i `Text` (`TMP_Text[]`).
- Wejście po Splashu:
  - `ResumePolicyController` nie aktywuje `hub_root`; po udanym wejściu woła `UIRouter.EnterHubByTab(UIRoute.Pvp)` (jakby user kliknął tab PvP).
  - `hub_root` traktowany jako legacy container i jest trzymany w stanie inactive (żeby nie “migał” w 1. klatce).
- PlayMode testy EPIC 11:
  - Resume policy: `aiwarsidle/Assets/Tests/PlayMode/Epic11ResumePolicyPlayModeTests.cs` + update gate: `aiwarsidle/Assets/Tests/PlayMode/Epic11UpdateRequiredPlayModeTests.cs`.
  - Router: `aiwarsidle/Assets/Tests/PlayMode/Epic11UIRouterPlayModeTests.cs` (w tym “przełączanie paneli nie gubi stanu”).
- Firebase Remote Config (reflection + IL2CPP preserve):
  - `FirebaseRemoteConfigMinAppVersion` + `FirebaseRemoteConfigStringValue` w `aiwarsidle/Assets/UI/Splash/`
  - `aiwarsidle/Assets/link.xml` zachowuje assembly Firebase pod reflection.
- Testy PlayMode EPIC 11:
  - Resume policy: `aiwarsidle/Assets/Tests/PlayMode/Epic11ResumePolicyPlayModeTests.cs`
  - Force update + download URL wiring: `aiwarsidle/Assets/Tests/PlayMode/Epic11UpdateRequiredPlayModeTests.cs`

---

## Overclock UI + HUD (update)

Zmiany zrobione w ramach dopięcia przycisku **Overclock AI** i feedbacku na HUD:

- Przycisk Overclock:
  - Controller: `aiwarsidle/Assets/UI/Generators/OverclockButtonController.cs`
    - Renderuje stan na jednym, oryginalnym TMP labelu (2 linie).
    - Stany:
      - idle: `OVERCLOCK` + `charges/max`
      - active: `ACTIVATED` + countdown sekund do końca
      - cooldown (gdy `charges < max`): `OVERCLOCK` + `charges/max MM:SS`
    - Włącza `Glow` (child o nazwie `Glow` lub przypięty ręcznie) i pulsuje nim tylko podczas aktywnego Overclock.
  - Wiring w scenie: `aiwarsidle/Assets/Scenes/SampleScene.unity` (button `button_overclock`).

- HUD PPS pulse podczas aktywnego Overclock:
  - `aiwarsidle/Assets/UI/Generators/HudController.cs` pulsuje sekcją PPS (label + wartość) tylko, gdy Overclock jest aktywny.

- Testy PlayMode (click-flow):
  - `aiwarsidle/Assets/Tests/PlayMode/Epic04OverclockAndUpgradePlayModeTests.cs`:
    - klik upgrade → rośnie level generatora i PPS na HUD
    - klik Overclock → PPS ×3 na czas aktywności i wraca po ~10s
  - asmdef: `aiwarsidle/Assets/Tests/PlayMode/AIWarsIdle.Tests.PlayMode.asmdef` (dopisane `Unity.TextMeshPro`)

Uruchamianie: Unity Test Runner → **PlayMode**.

---

## Offline claim modal (STORY 11.2b)

Zmiany zrobione w ramach dopięcia offline claim z UI:

- Modal offline claim:
  - Controller: `aiwarsidle/Assets/UI/Generators/OfflineClaimModalController.cs`
  - Pokazuje `claim_offline_root` po wejściu do gry, gdy `PendingOfflineGain > 0`.
  - Wypełnia GUI:
    - `offline_gain_number` kwotą rewarda w tym samym compact formacie co reszta HUD,
    - `offline_time_description` w formacie `Offline Time: 3h 25m 43s (CAP: 12h)`.
  - Obsługuje akcje:
    - `Claim` → `OfflineClaimService.Claim(1)`
    - `Claim x2` → rewarded stub (`offline_x2`) + `OfflineClaimService.Claim(2)`
    - `Not now` → zamyka modal bez claim, pending zostaje.

- HUD button do ponownego otwierania:
  - Istniejący `button_claim_offline` z HUD otwiera ten sam modal.
  - Jest aktywny tylko wtedy, gdy istnieje reward możliwy do odebrania (`PendingOfflineGain > 0`).
  - Pokrywa use case:
    - gracz wybrał `Claim later`,
    - gracz wrócił do gry bez przejścia przez Splash (resume krótszy niż timeout).

- Core / persistence pod modal:
  - `OfflineClaimService` przechowuje też metadane ostatniego bankowania offline:
    - raw offline seconds,
    - effective offline seconds po capie.
  - Dane są mapowane do save, żeby UI mogło poprawnie pokazać czas offline po restarcie aplikacji.

- Offline efficiency:
  - Dodane pole `OfflineEfficiency` do `BalanceConfig` w `aiwarsidle/Assets/GameCore/Config/BalanceConfig.cs`.
  - Aktualna wartość domyślna: `0.6`.
  - Offline gain jest teraz liczony jako:
    - `CalculateProductionPerSecond() * seconds * OfflineEfficiency`
  - Oznacza to, że offline zarobek jest celowo mniej efektywny niż aktywna gra.

- Testy:
  - EditMode:
    - `aiwarsidle/Assets/Tests/EditMode/Epic10OfflineClaimServiceTests.cs`
    - `aiwarsidle/Assets/Tests/EditMode/Epic7RewardedAdsUseCaseTests.cs`
    - `aiwarsidle/Assets/Tests/EditMode/DomainModelValidationTests.cs`
  - PlayMode:
    - `aiwarsidle/Assets/Tests/PlayMode/Epic11OfflineClaimModalPlayModeTests.cs`
    - pokryte: auto-show, claim x1, claim x2, `Not now`, reopen z HUD.

---

## PvP map combat model update

Zmiany zrobione w ramach uporządkowania warstwy testowego PvE na mapie przed docelowym PvP z innymi graczami:

- Obrona sektorów:
  - sektor przestał bronić się historycznym `OwnerSnapshot` zapisanym w momencie przejęcia,
  - `MatchmakingService` korzysta teraz z bieżącej mocy właściciela frakcji, a nie z zamrożonego snapshotu,
  - dzięki temu wynik walki lepiej odpowiada aktualnej sile gracza / frakcji i jest czytelniejszy produktowo.

- Niezależna moc botów:
  - boty przestały skalować się bezpośrednio od aktualnego snapshotu lokalnego gracza,
  - dodany został wspólny serwis `aiwarsidle/Assets/PvP/Services/FactionSnapshotService.cs`,
  - moc frakcji botów jest teraz liczona z ich własnego stanu mapy:
    - liczby home-connected sektorów,
    - profilu frakcji,
    - bonusów sektorowych PvP,
    - skonfigurowanego zakresu `BotPowerMinMultiplier` / `BotPowerMaxMultiplier`,
  - celem tej zmiany było przywrócenie realnego payoffu progresji idle i uniknięcie efektu „bot zawsze utrzymuje ten sam dystans power”.

- Wiring runtime:
  - `aiwarsidle/Assets/Bootstrap/GameLoop.cs` tworzy i wstrzykuje `FactionSnapshotService` do:
    - `MatchmakingService`
    - `PvpBotService`

- Testy EditMode:
  - `aiwarsidle/Assets/Tests/EditMode/Epic6MapCoreTests.cs`
  - dopisane scenariusze pilnujące, że:
    - matchmaking nie używa już zamrożonego `OwnerSnapshot` jako głównego źródła obrony,
    - moc bota nie zależy od bieżącego snapshotu lokalnego gracza,
    - poprawiony został też test resetu bucketów botów po `DebugResetMatch`, tak aby sprawdzał brak dodatkowego ataku w tym samym buckecie zamiast sztywnej liczby przejętych sektorów.

- Build / weryfikacja:
  - `dotnet build aiwarsidle/AIWarsIdle.Tests.EditMode.csproj -c Release -v minimal`
  - build przechodzi po zmianach; `dotnet test` w tym repo nie wypisywał wiarygodnego podsumowania test runnera, więc podstawową weryfikacją był czysty build projektu testowego i nowe testy regresyjne.
