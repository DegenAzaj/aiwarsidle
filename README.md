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
