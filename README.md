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
