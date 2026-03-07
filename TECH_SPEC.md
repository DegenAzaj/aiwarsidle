# TECH_SPEC.md

## Project: AI Wars Idle (MVP v1)

---

# 1. Założenia techniczne

* Silnik: Unity
* Architektura: modularna, serwisowa
* Logika biznesowa oddzielona od UI
* Wszystkie liczby balansowe w konfiguracji (ScriptableObjects)
* Deterministyczne obliczenia ekonomii
* SaveData wersjonowany
* PvP symulowane (brak real-time)
* MVP: PvP realizowane jako PvE (gracz vs 5 botów) na wspólnej mapie
* PvP oparte o heksagonalną mapę sektorów (`hex grid`)

---

# 2. Struktura projektu

```
/GameCore
    EconomyService.cs
    ProductionService.cs
    UpgradeService.cs
    PrestigeService.cs
    GameState.cs

/PvP
    SnapshotService.cs
    MapService.cs
    MatchmakingService.cs
    BattleSimService.cs
    LeagueService.cs
    LeaderboardService.cs

/Monetization
    AdsService.cs
    SubscriptionService.cs

/Data
    BalanceConfig.asset
    PvpConfig.asset
    MapConfig.asset
    LeagueConfig.asset

/Persistence
    SaveData.cs
    SaveService.cs

/Analytics
    AnalyticsService.cs

/UI
    UIEvents.cs
    UIRouter.cs
```

---

# 3. Game State

## 3.1 Runtime State (GameState)

Przechowuje:

* SoftCurrency (double)
* PremiumCurrency (int)
* GeneratorsLevels[5]
* GlobalMultiplier (float)
* PrestigeCount (int)
* PermanentUpgradeLevel (int)
* LeagueSeasonId (int) — sezon ligi (30 dni)
* CurrentLeague
* SeasonPoints (int) — punkty w bieżącym sezonie ligi
* PvPAttacksRemaining (int) — 0..MaxAttacks
* LastPvpAttackRegenTimestamp (long)
* LastPvpAdAttackClaimTimestamp (long) — 1× dziennie
* MapState (patrz 5.2)
* LastLoginTimestamp

GameState jest jedynym źródłem prawdy.

---

# 4. Core Systems

---

## 4.1 ProductionService

Odpowiedzialność:

* Obliczanie produkcji per second
* Tick co 1s
* Offline progression

Metody:

```
double CalculateProductionPerSecond()
double CalculateBaseProductionPerSecondWithoutSubscription()
double CalculateOfflineGain(double seconds)
void ApplyProduction(double deltaTime)
```

Offline cap: 12h (config).

---

## 4.2 UpgradeService

Odpowiedzialność:

* Koszt następnego poziomu
* Wzrost produkcji

Koszt:

```
cost = baseCost * pow(growthFactor, level)
```

Metody:

```
double GetUpgradeCost(int generatorId)
void UpgradeGenerator(int generatorId)
```

---

## 4.3 PrestigeService

Warunek:

* TotalEarnedCurrency >= prestigeThreshold

Gdzie:

* `prestigeThreshold = BalanceConfig.PrestigeThresholdBase * pow(BalanceConfig.PrestigeThresholdGrowthFactor, PrestigeCount)`

Efekt:

* Reset generator levels
* Permanent progress: `PermanentUpgradeLevel++` (clamp do max 10)
* Recalculate `GlobalMultiplier` na podstawie `PermanentUpgradeLevel` (config), np.:
  * `GlobalMultiplier = 1 + (PermanentUpgradeLevel * BalanceConfig.PrestigeMultiplierIncrease)`

Metody:

```
bool CanPrestige()
void ExecutePrestige()
```

---

## 4.4 EconomyService

Odpowiada za:

* AddCurrency
* SpendCurrency (walidacja)
* Event dispatch do UI

Nie ma logiki UI.

---

# 5. Map PvP System (MVP PvE vs boty)

---

## 5.1 SnapshotService

Tworzy obiekt:

```
class PvpSnapshot {
    double PvpPower;
    int League;
    int SeasonPoints;
}
```

`PvpPower` to statystyka stricte do walki (nie jest tym samym co `ProductionPps` używane w ekonomii).

Definicje:

* `ProductionPps` — baza pod ekonomię (tu działa subskrypcja ×2)
* `PvpPower` — stat do walki (nie dostaje ×2 bezpośrednio z subskrypcji)

Proponowany model (MVP, konfiguracja w `PvpConfig`):

```
core = 1
  + (PrestigeCount * PrestigePvpPowerPerPrestige)
  + (PermanentUpgradeLevel * PermanentPvpPowerPerLevel)
  + (EffectiveSectorCount * SectorPvpPowerPerSector)

prodPpsForPvp = BaseProductionPpsWithoutSubscription
prodBonus = ProdToPvpMaxBonus * (prodPpsForPvp / (prodPpsForPvp + ProdToPvpHalfCapPps))

PvpPower = core * (1 + prodBonus)
```

`ProductionPps` powinno zawierać wszystkie stałe mnożniki ekonomii (prestige/global, sektory, subskrypcja itp.).
Tymczasowe boosty PvP (np. Overclock +% attack power) stosuj osobno w PvP (np. jako dodatkowy multiplier w `BattleSimService`), żeby nie mieszać ich z ekonomią.

---

## 5.2 MapService

MapService zarządza stanem mapy heksagonalnej i sektorów (właściciel, stability, bonusy, cooldown).

Model (MVP):

```
enum AttackStrategy {
    Aggressive,
    Stable,
    Risky
}

class SectorState {
    int SectorId;
    int OwnerPlayerId;      // MVP: 0 = neutral / bot / unknown
    PvpSnapshot OwnerSnapshot;
    float Stability;        // 0..100
    long LastCombatTimestamp;
}

class MapState {
    int MapSeasonId; // reset co ~72h
    SectorState[] Sectors;
}

class AttackPreview {
    int SectorId;
    AttackStrategy Strategy;
    float WinChanceMin;  // widełki do UI (przybliżone)
    float WinChanceMax;
}

class CombatResult {
    int SectorId;
    bool Win;
    BattleResult Battle;
    SectorState UpdatedSector;
}
```

Zasady (MVP):

* Mapa ma heksagonalny układ w kształcie dużego hexa: `radius = 4`, ok. 61 sektorów.
* W konflikcie uczestniczy 6 stron: gracz + 5 botów.
* Każda strona startuje z jednego narożnego `Home Sector`.
* `Home Sector` jest nietykalny i nie może zostać przejęty.
* Większość mapy startuje jako neutralna.
* Zasada mapy: **brak teleportu** — atak jest możliwy tylko na sektor sąsiedni (połączenia z `MapConfig`).
* Po przejęciu sektor startuje z niską stabilnością (config).
* Stabilność rośnie w czasie (config).
* `OwnerSnapshot` służy do obrony; w MVP źródłem przeciwników są boty lub zapisany snapshot bot-owner-a.
* Boty działają okresowo i używają tych samych zasad co gracz, ale mogą mieć różne profile zachowań.

Metody:

```
MapState GetState()
SectorState GetSector(int sectorId)
void TickStability(long nowTimestamp)
AttackPreview GetAttackPreview(int sectorId, AttackStrategy strategy)
CombatResult AttackSector(int sectorId, AttackStrategy strategy)
void ResetMapSeasonIfNeeded(long nowTimestamp)
```

`AttackPreview` zwraca m.in. widełki szansy (bez obietnicy 100% dokładności).

---

## 5.3 MatchmakingService

Wejście:

* AttackerSnapshot
* DefenderSector / DefenderPvpPower

Wyjście:

* Snapshot obrońcy (OwnerSnapshot)

Logika:

* Dla sektora neutralnego lub bez wiarygodnego ownera: generuj bot snapshot w zakresie 80–120% `PvpPower` atakującego (config).
* Dla sektora zajętego przez bota: użyj zapisanej wartości `OwnerSnapshot`.
* Backend async PvP jest poza MVP.

---

## 5.4 BattleSimService

Metoda:

```
BattleResult Simulate(PvpSnapshot attacker, PvpSnapshot defender, AttackStrategy strategy, float defenderStability)
```

Algorytm:

```
attackRoll = attacker.PvpPower * StrategyMultiplier(strategy) * Random(VarianceMin..VarianceMax)
defenseRoll = defender.PvpPower * StabilityMultiplier(defenderStability) * Random(VarianceMin..VarianceMax)
```

Porównanie → Win / Lose (Win = przejęcie sektora)

Zwraca:

* bool Win
* int LeaguePointsDelta
* double SoftReward

---

## 5.5 LeagueService

Odpowiedzialność:

* Dodawanie punktów
* Awans/spadek ligi
* Reset sezonu ligi (30 dni) po dacie / seasonId

LeagueConfig:

* Progi punktowe
* Punkty za win/lose

---

## 5.6 LeaderboardService

Na MVP:

* Lokalna lista top100 (mock)

Docelowo:

* Backend endpoint

---

# 6. Monetization

---

## 6.1 AdsService

Funkcje:

```
ShowRewardedAd(Action onSuccess)
```

Użycie:

* x2 offline claim
* +1 PvP attack (dodatkowy atak / dzień)

---

## 6.2 SubscriptionService

Stan:

* bool IsActive

Efekty:

* Production multiplier ×2
* Disable interstitial ads (rewarded ads zostają jako opcjonalne placementy)

Weryfikacja store po stronie platformy.

---

# 7. Persistence

---

## 7.1 SaveData

```
class SaveDataV1 {
    int Version = 1;
    double SoftCurrency;
    int PremiumCurrency;
    int[] GeneratorsLevels;
    int PrestigeCount;
    int PermanentUpgradeLevel;
    int LeagueSeasonId;
    int League;
    int SeasonPoints;
    int PvpAttacksRemaining;
    long LastPvpAttackRegenTimestamp;
    long LastPvpAdAttackClaimTimestamp;
    int MapSeasonId;
    SectorSaveData[] Sectors;
    long LastLoginTimestamp;
}

class SectorSaveData {
    int SectorId;
    int OwnerPlayerId;
    double OwnerPvpPower;
    int OwnerLeague;
    int OwnerSeasonPoints;
    float Stability;
    long LastCombatTimestamp;
}
```

---

## 7.2 SaveService

* Auto save co 30s
* Save on app pause/quit
* Load z migracją wersji

---

# 8. Balance Configuration

ScriptableObject:

## BalanceConfig

* GeneratorBaseCosts[5]
* GeneratorGrowthFactors[5]
* GeneratorBaseProduction[5]
* PrestigeThresholdBase
* PrestigeThresholdGrowthFactor
* PrestigeMultiplierIncrease
* PermanentUpgradeMaxLevel (10)
* OfflineCapSeconds

## PvpConfig

* PowerVarianceMin
* PowerVarianceMax
* StrategyMultiplierAggressive
* StrategyMultiplierStable
* StrategyMultiplierRisky
* StabilityMultiplierMin
* StabilityMultiplierMax
* MaxAttacks (5)
* AttackRegenSeconds (7200)
* AdExtraAttacksPerDay
* PremiumExtraAttacksPerPurchase (config)
* WinPoints
* LosePoints
* RewardMultiplier
* PrestigePvpPowerPerPrestige
* PermanentPvpPowerPerLevel
* SectorPvpPowerPerSector
* ProdToPvpMaxBonus
* ProdToPvpHalfCapPps

## MapConfig

* MapSeasonLengthDays (3 dla MVP, tj. ok. 72h)
* Adjacency (np. lista połączeń sektorów / sąsiedztwa)
* Sectors[] (id, name, productionBonusPercent, pvpPowerBonusValue lub pvpPowerBonusPercent)
* SectorBonusCapPercent (cap sumy bonusów) lub DiminishingReturnsThresholdPercent
* StabilityStartOnCapture
* StabilityGrowthPerHour
* StabilityMax (100)
* SectorAttackCooldownSeconds (opcjonalnie MVP)

---

# 9. Analytics

AnalyticsService wysyła eventy:

```
Track("session_start")
Track("offline_claim", amount)
Track("generator_upgrade", id, level)
Track("prestige", count)
Track("map_open")
Track("sector_view", sectorId)
Track("sector_attack", sectorId, strategy)
Track("sector_result", sectorId, win)
Track("ad_watched")
Track("subscription_started")
```

Nie blokuje gameplay.

---

# 10. Wymagania jakościowe

* Zero logiki ekonomii w UI
* Wszystkie liczby w configu
* Deterministyczne obliczenia
* Brak singletonów zależnych od MonoBehaviour (poza bootstrapem)
* Możliwość testowania serwisów bez UI

---

# 11. Poza zakresem MVP

* Artefakty / gacha
* Dynamiczne oferty
* Real backend PvP (server-side resolution + leaderboard)
* Obrona/tarcze
* Battle pass
* Gildie / sojusze / czat
* Rozbudowane drzewko ascension

---

# 12. Definition of Done

Feature jest gotowy gdy:

* Działa w runtime
* Jest zapisywany w SaveData
* Ma telemetrię
* Ma konfigurację w ScriptableObject
* Nie wymaga zmian w UI do działania

---
