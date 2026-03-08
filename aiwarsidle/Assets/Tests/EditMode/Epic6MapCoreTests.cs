using AIWarsIdle.GameCore.Config;
using AIWarsIdle.GameCore.Domain;
using AIWarsIdle.GameCore.Services;
using AIWarsIdle.PvP.Config;
using AIWarsIdle.PvP.Services;
using NUnit.Framework;
using UnityEngine;

namespace AIWarsIdle.Tests.EditMode
{
    public sealed class Epic6MapCoreTests
    {
        private static BalanceConfig CreateValidBalanceConfig()
        {
            var cfg = ScriptableObject.CreateInstance<BalanceConfig>();
            for (var i = 0; i < GameState.GeneratorCount; i++)
            {
                cfg.GeneratorBaseCosts[i] = 1;
                cfg.GeneratorCostGrowthFactors[i] = 1.2;
                cfg.GeneratorBaseOutputs[i] = 100;
            }

            cfg.MilestoneEveryLevels = 0;
            cfg.PrestigeThresholdBase = 1_000;
            cfg.PrestigeThresholdGrowthFactor = 1.6;
            cfg.PrestigeMultiplierIncrease = 0.0;
            cfg.PermanentUpgradeCap = 10;
            cfg.OfflineCapSeconds = 12 * 60 * 60;
            cfg.ValidateOrThrow();
            return cfg;
        }

        private static PvpConfig CreateDeterministicPvpConfig(double prestigePowerPerPrestige)
        {
            var cfg = ScriptableObject.CreateInstance<PvpConfig>();
            cfg.PowerVarianceMin = 1.0f;
            cfg.PowerVarianceMax = 1.0f;
            cfg.StrategyMultiplierAggressive = 1.0f;
            cfg.StrategyMultiplierStable = 1.0f;
            cfg.StrategyMultiplierRisky = 1.0f;
            cfg.StabilityMultiplierMin = 1.0f;
            cfg.StabilityMultiplierMax = 1.0f;
            cfg.DefenseBonus = 1.0f;
            cfg.FlankBonusPerExtraAttacker = 0.0f;
            cfg.FlankBonusMaxMultiplier = 1.0f;
            cfg.CombatMaintenanceFreeSectors = 999;
            cfg.CombatMaintenancePenaltyPerExtraSector = 0.0f;
            cfg.CombatMaintenanceMinMultiplier = 1.0f;
            cfg.UnderdogMaxAttackBonus = 0.0f;
            cfg.UnderdogSectorDeficitForMaxBonus = 1;

            cfg.PrestigePvpPowerPerPrestige = prestigePowerPerPrestige;
            cfg.PermanentPvpPowerPerLevel = 0.0;
            cfg.SectorPvpPowerPerSector = 0.0;
            cfg.ProdToPvpMaxBonus = 0.0;
            cfg.ProdToPvpHalfCapPps = 1.0;
            cfg.ValidateOrThrow();
            return cfg;
        }

        private static PvpAttacksConfig CreateDefaultPvpAttacksConfig(int maxAttacks = 5, int regenSeconds = 2 * 60 * 60)
        {
            var cfg = ScriptableObject.CreateInstance<PvpAttacksConfig>();
            cfg.MaxAttacks = maxAttacks;
            cfg.RegenSeconds = regenSeconds;
            cfg.AdExtraAttacksPerDay = 1;
            cfg.PremiumExtraAttacksPerPurchase = 0;
            cfg.PremiumCurrencyCostPerPurchase = 0;
            cfg.ValidateOrThrow();
            return cfg;
        }

        private static LeagueConfig CreateValidLeagueConfig()
        {
            var cfg = ScriptableObject.CreateInstance<LeagueConfig>();
            cfg.LeaguePointThresholds = new[] { 0, 100, 200 };
            cfg.SeasonMode = LeagueSeasonMode.FixedDays;
            cfg.FixedSeasonLengthDays = 28;
            cfg.FixedSeasonAnchorUnixSecondsUtc = 0;
            cfg.ValidateOrThrow();
            return cfg;
        }

        private static MapConfig CreateSmallMapConfig(float botMult, float stabilityStartNeutral = 0f)
        {
            var cfg = ScriptableObject.CreateInstance<MapConfig>();
            cfg.MapSeasonLengthDays = 7;
            cfg.MapSeasonAnchorUnixSecondsUtc = 0;
            cfg.LocalPlayerId = 1;
            cfg.HomeSectorId = 0;
            cfg.HomeSectorStability = 100f;

            cfg.EnforceSectorCountRange = false;
            cfg.RequireConnectedGraph = true;

            cfg.SectorDefinitions = new[]
            {
                new MapConfig.SectorDefinition { SectorId = 0, Name = "Home", ProductionBonusPercent = 5f, PvpPowerBonusFlat = 0.0, PvpPowerBonusPercent = 0f },
                new MapConfig.SectorDefinition { SectorId = 1, Name = "A", ProductionBonusPercent = 5f, PvpPowerBonusFlat = 0.0, PvpPowerBonusPercent = 0f },
                new MapConfig.SectorDefinition { SectorId = 2, Name = "B", ProductionBonusPercent = 5f, PvpPowerBonusFlat = 0.0, PvpPowerBonusPercent = 0f },
            };

            cfg.Adjacency = new[]
            {
                new MapConfig.SectorEdge { A = 0, B = 1 },
                new MapConfig.SectorEdge { A = 1, B = 2 }
            };

            cfg.StabilityGrowthPerSecond = 0f;
            cfg.FreshCaptureWindowSeconds = 60;
            cfg.StabilityStartNeutral = stabilityStartNeutral;
            cfg.StabilityStartOnCapture = 10f;
            cfg.StabilityGainOnDefenseWin = 5f;
            cfg.CaptureStabilityMultiplierAggressive = 0.8f;
            cfg.CaptureStabilityMultiplierStable = 1.0f;
            cfg.CaptureStabilityMultiplierRisky = 0.6f;
            cfg.SectorAttackCooldownSeconds = 0;

            cfg.BotPowerMinMultiplier = botMult;
            cfg.BotPowerMaxMultiplier = botMult;
            cfg.NeutralPowerMinMultiplier = 0.5f;
            cfg.NeutralPowerMaxMultiplier = 0.5f;

            cfg.WinSeasonPoints = 10;
            cfg.LoseSeasonPoints = -2;
            cfg.WinSoftReward = 50;
            cfg.LoseSoftReward = 10;

            cfg.SectorBonusCapPercent = 0;
            cfg.MaintenanceFreeSectors = 5;
            cfg.MaintenancePenaltyPercentPerExtraSector = 1f;
            cfg.MaintenanceMinMultiplier = 0.1f;

            cfg.ValidateOrThrow();
            return cfg;
        }

        private static EconomyService CreateEconomyWithState(GameState state)
        {
            return new EconomyService(state);
        }

        [Test]
        public void ResetMapSeasonIfNeeded_Initializes_Home_And_Neutrals()
        {
            var state = new GameState();
            var mapCfg = CreateSmallMapConfig(botMult: 1.0f, stabilityStartNeutral: 7f);
            var map = new MapService(state.MapState, mapCfg);

            map.AdvanceTime(nowUnixSeconds: 1000);

            var home = map.GetSector(0);
            var a = map.GetSector(1);
            var b = map.GetSector(2);

            Assert.NotNull(home);
            Assert.NotNull(a);
            Assert.NotNull(b);

            Assert.AreEqual(1, home.OwnerPlayerId);
            Assert.AreEqual(100f, home.Stability);

            Assert.AreEqual(0, a.OwnerPlayerId);
            Assert.AreEqual(7f, a.Stability);
            Assert.AreEqual(0, b.OwnerPlayerId);
            Assert.AreEqual(7f, b.Stability);
        }

        [Test]
        public void MapConfig_Validation_Enforces_SectorCount_WhenEnabled()
        {
            var cfg = CreateSmallMapConfig(botMult: 1.0f);
            cfg.EnforceSectorCountRange = true;
            cfg.MinSectorCount = 20;
            cfg.MaxSectorCount = 30;

            Assert.Throws<System.InvalidOperationException>(() => cfg.ValidateOrThrow());
        }

        [Test]
        public void MapConfig_Validation_ConnectedGraph_Rejects_Islands()
        {
            var cfg = ScriptableObject.CreateInstance<MapConfig>();
            cfg.LocalPlayerId = 1;
            cfg.HomeSectorId = 0;
            cfg.HomeSectorStability = 100f;
            cfg.EnforceSectorCountRange = false;
            cfg.RequireConnectedGraph = true;

            cfg.SectorDefinitions = new[]
            {
                new MapConfig.SectorDefinition { SectorId = 0, Name = "Home" },
                new MapConfig.SectorDefinition { SectorId = 1, Name = "A" },
                new MapConfig.SectorDefinition { SectorId = 2, Name = "Island" },
            };
            cfg.Adjacency = new[]
            {
                new MapConfig.SectorEdge { A = 0, B = 1 }
            };

            Assert.Throws<System.InvalidOperationException>(() => cfg.ValidateOrThrow());
        }

        [Test]
        public void TickStability_Grows_And_Clamps_To100()
        {
            var state = new GameState();
            var cfg = CreateSmallMapConfig(botMult: 1.0f);
            cfg.StabilityGrowthPerSecond = 10f;
            cfg.ValidateOrThrow();

            var map = new MapService(state.MapState, cfg);
            map.AdvanceTime(nowUnixSeconds: 1000);

            var sector = map.GetSector(1);
            sector.Stability = 95f;

            map.TickStability(nowUnixSeconds: 1000); // baseline init
            map.TickStability(nowUnixSeconds: 1001);

            Assert.AreEqual(100f, sector.Stability);
        }

        [Test]
        public void ResetMapSeasonIfNeeded_NewSeason_Resets_NeutralSectors_ButKeepsHomeOwned()
        {
            var state = new GameState();
            var cfg = CreateSmallMapConfig(botMult: 1.0f);
            var map = new MapService(state.MapState, cfg);

            map.AdvanceTime(nowUnixSeconds: 10);
            var a = map.GetSector(1);
            a.OwnerPlayerId = 99;
            a.Stability = 50f;

            var nextSeason = 10 + (8L * 24L * 60L * 60L); // >7 days
            map.AdvanceTime(nowUnixSeconds: nextSeason);

            Assert.AreEqual(1, map.GetSector(0).OwnerPlayerId);
            Assert.AreEqual(0, map.GetSector(1).OwnerPlayerId);
            Assert.AreEqual(cfg.StabilityStartNeutral, map.GetSector(1).Stability);
        }

        [Test]
        public void DebugResetCurrentMatch_ResetsMapToSeasonStart()
        {
            var state = new GameState();
            var cfg = CreateSmallMapConfig(botMult: 1.0f, stabilityStartNeutral: 7f);
            var map = new MapService(state.MapState, cfg);

            map.AdvanceTime(1000);

            var neutral = map.GetSector(1);
            neutral.OwnerPlayerId = 3;
            neutral.Stability = 44f;
            neutral.CapturedUnixSeconds = 999;
            neutral.LastCombatUnixSeconds = 998;

            map.DebugResetCurrentMatch(2000);

            Assert.AreEqual(1, map.GetSector(0).OwnerPlayerId);
            Assert.AreEqual(100f, map.GetSector(0).Stability);
            Assert.AreEqual(0, map.GetSector(1).OwnerPlayerId);
            Assert.AreEqual(cfg.StabilityStartNeutral, map.GetSector(1).Stability);
            Assert.AreEqual(0, map.GetSector(1).CapturedUnixSeconds);
            Assert.AreEqual(0, map.GetSector(1).LastCombatUnixSeconds);
        }

        [Test]
        public void AttackSector_Fails_When_NotAdjacent_And_DoesNotSpendAttack()
        {
            var state = new GameState { PvpAttacksRemaining = 1 };
            var mapCfg = CreateSmallMapConfig(botMult: 1.0f);
            var map = new MapService(state.MapState, mapCfg);
            map.AdvanceTime(nowUnixSeconds: 1000);

            var economy = CreateEconomyWithState(state);
            var balance = CreateValidBalanceConfig();
            var production = new ProductionService(state, balance, economy);
            var pvp = CreateDeterministicPvpConfig(prestigePowerPerPrestige: 99);
            state.PrestigeCount = 1;

            var snapshot = new SnapshotService(state, pvp, production, mapConfig: mapCfg);
            var league = new LeagueService(state, CreateValidLeagueConfig());
            var sim = new BattleSimService(pvp);
            var matchmaking = new MatchmakingService(mapCfg);
            var attacks = new PvpAttackChargesService(state, CreateDefaultPvpAttacksConfig());
            var combat = new PvpMapCombatService(state, map, mapCfg, attacks, snapshot, matchmaking, sim, economy, league);

            var before = map.GetSector(2);
            var beforeOwner = before.OwnerPlayerId;
            var beforeStability = before.Stability;

            var result = combat.AttackSector(sectorId: 2, strategy: AttackStrategy.Stable, nowUnixSeconds: 1001, seed: 123);

            Assert.IsFalse(result.Win);
            Assert.AreEqual(1, state.PvpAttacksRemaining);

            var after = map.GetSector(2);
            Assert.AreEqual(beforeOwner, after.OwnerPlayerId);
            Assert.AreEqual(beforeStability, after.Stability);
        }

        [Test]
        public void AttackSector_Fails_When_Adjacent_Only_To_Island_And_DoesNotSpendAttack()
        {
            var state = new GameState { PvpAttacksRemaining = 1 };
            var mapCfg = ScriptableObject.CreateInstance<MapConfig>();
            mapCfg.MapSeasonLengthDays = 7;
            mapCfg.MapSeasonAnchorUnixSecondsUtc = 0;
            mapCfg.LocalPlayerId = 1;
            mapCfg.HomeSectorId = 0;
            mapCfg.HomeSectorIds = new[] { 0 };
            mapCfg.HomeSectorOwnerPlayerIds = new[] { 1 };
            mapCfg.HomeSectorStability = 100f;
            mapCfg.EnforceSectorCountRange = false;
            mapCfg.RequireConnectedGraph = false;
            mapCfg.SectorDefinitions = new[]
            {
                new MapConfig.SectorDefinition { SectorId = 0, Name = "Home" },
                new MapConfig.SectorDefinition { SectorId = 1, Name = "Bridge" },
                new MapConfig.SectorDefinition { SectorId = 2, Name = "Island" },
                new MapConfig.SectorDefinition { SectorId = 3, Name = "IslandFrontier" },
            };
            mapCfg.Adjacency = new[]
            {
                new MapConfig.SectorEdge { A = 0, B = 1 },
                new MapConfig.SectorEdge { A = 1, B = 2 },
                new MapConfig.SectorEdge { A = 2, B = 3 },
            };
            mapCfg.StabilityGrowthPerSecond = 0f;
            mapCfg.FreshCaptureWindowSeconds = 60;
            mapCfg.StabilityStartNeutral = 0f;
            mapCfg.StabilityStartOnCapture = 10f;
            mapCfg.StabilityGainOnDefenseWin = 5f;
            mapCfg.CaptureStabilityMultiplierAggressive = 0.8f;
            mapCfg.CaptureStabilityMultiplierStable = 1.0f;
            mapCfg.CaptureStabilityMultiplierRisky = 0.6f;
            mapCfg.SectorAttackCooldownSeconds = 0;
            mapCfg.BotPowerMinMultiplier = 1f;
            mapCfg.BotPowerMaxMultiplier = 1f;
            mapCfg.WinSeasonPoints = 10;
            mapCfg.LoseSeasonPoints = -2;
            mapCfg.WinSoftReward = 50;
            mapCfg.LoseSoftReward = 10;
            mapCfg.ValidateOrThrow();

            state.MapState.Sectors = new[]
            {
                new SectorState { SectorId = 0, OwnerPlayerId = 1, Stability = 100f },
                new SectorState { SectorId = 1, OwnerPlayerId = 0, Stability = 0f },
                new SectorState { SectorId = 2, OwnerPlayerId = 1, Stability = 100f },
                new SectorState { SectorId = 3, OwnerPlayerId = 0, Stability = 0f },
            };

            var map = new MapService(state.MapState, mapCfg);
            map.AdvanceTime(nowUnixSeconds: 1000);

            var economy = CreateEconomyWithState(state);
            var balance = CreateValidBalanceConfig();
            var production = new ProductionService(state, balance, economy);
            var pvp = CreateDeterministicPvpConfig(prestigePowerPerPrestige: 99);
            state.PrestigeCount = 1;

            var snapshot = new SnapshotService(state, pvp, production, mapConfig: mapCfg);
            var league = new LeagueService(state, CreateValidLeagueConfig());
            var sim = new BattleSimService(pvp);
            var matchmaking = new MatchmakingService(mapCfg);
            var attacks = new PvpAttackChargesService(state, CreateDefaultPvpAttacksConfig());
            var combat = new PvpMapCombatService(state, map, mapCfg, attacks, snapshot, matchmaking, sim, economy, league);

            var evaluation = combat.EvaluateAttack(3, 1001);
            Assert.IsFalse(evaluation.CanAttack);
            Assert.AreEqual(PvpAttackBlockReason.NoAdjacentOwnedSector, evaluation.BlockReason);

            var result = combat.AttackSector(sectorId: 3, strategy: AttackStrategy.Stable, nowUnixSeconds: 1001, seed: 123);
            Assert.IsFalse(result.Win);
            Assert.AreEqual(1, state.PvpAttacksRemaining);
            Assert.AreEqual(0, map.GetSector(3).OwnerPlayerId);
        }

        [Test]
        public void AttackSector_Fails_When_NoAttacksRemaining_AndDoesNotMutateSector()
        {
            var state = new GameState { PvpAttacksRemaining = 0 };
            var mapCfg = CreateSmallMapConfig(botMult: 1.0f);
            var map = new MapService(state.MapState, mapCfg);
            map.AdvanceTime(nowUnixSeconds: 1000);

            var economy = CreateEconomyWithState(state);
            var balance = CreateValidBalanceConfig();
            var production = new ProductionService(state, balance, economy);
            var pvp = CreateDeterministicPvpConfig(prestigePowerPerPrestige: 99);
            state.PrestigeCount = 1;

            var snapshot = new SnapshotService(state, pvp, production, mapConfig: mapCfg);
            var league = new LeagueService(state, CreateValidLeagueConfig());
            var sim = new BattleSimService(pvp);
            var matchmaking = new MatchmakingService(mapCfg);
            var attacks = new PvpAttackChargesService(state, CreateDefaultPvpAttacksConfig());
            var combat = new PvpMapCombatService(state, map, mapCfg, attacks, snapshot, matchmaking, sim, economy, league);

            var before = map.GetSector(1);
            var beforeOwner = before.OwnerPlayerId;
            var beforeStability = before.Stability;
            var beforeLastCombat = before.LastCombatUnixSeconds;

            var result = combat.AttackSector(sectorId: 1, strategy: AttackStrategy.Stable, nowUnixSeconds: 1001, seed: 1);

            Assert.IsFalse(result.Win);
            Assert.AreEqual(0, state.PvpAttacksRemaining);
            Assert.AreEqual(0.0, economy.Balance);

            var after = map.GetSector(1);
            Assert.AreEqual(beforeOwner, after.OwnerPlayerId);
            Assert.AreEqual(beforeStability, after.Stability);
            Assert.AreEqual(beforeLastCombat, after.LastCombatUnixSeconds);
        }

        [Test]
        public void AttackSector_Fails_When_OnCooldown_AndDoesNotSpendAttack()
        {
            var state = new GameState { PvpAttacksRemaining = 1 };
            var mapCfg = CreateSmallMapConfig(botMult: 1.0f);
            mapCfg.SectorAttackCooldownSeconds = 60;
            mapCfg.ValidateOrThrow();

            var map = new MapService(state.MapState, mapCfg);
            map.AdvanceTime(nowUnixSeconds: 1000);
            map.GetSector(1).LastCombatUnixSeconds = 980;

            var economy = CreateEconomyWithState(state);
            var balance = CreateValidBalanceConfig();
            var production = new ProductionService(state, balance, economy);
            var pvp = CreateDeterministicPvpConfig(prestigePowerPerPrestige: 99);
            state.PrestigeCount = 1;

            var snapshot = new SnapshotService(state, pvp, production, mapConfig: mapCfg);
            var league = new LeagueService(state, CreateValidLeagueConfig());
            var sim = new BattleSimService(pvp);
            var matchmaking = new MatchmakingService(mapCfg);
            var attacks = new PvpAttackChargesService(state, CreateDefaultPvpAttacksConfig());
            var combat = new PvpMapCombatService(state, map, mapCfg, attacks, snapshot, matchmaking, sim, economy, league);

            var before = map.GetSector(1);
            var beforeLastCombat = before.LastCombatUnixSeconds;

            var result = combat.AttackSector(sectorId: 1, strategy: AttackStrategy.Stable, nowUnixSeconds: 1001, seed: 1);

            Assert.IsFalse(result.Win);
            Assert.AreEqual(1, state.PvpAttacksRemaining);
            Assert.AreEqual(beforeLastCombat, map.GetSector(1).LastCombatUnixSeconds);
        }

        [Test]
        public void AttackSector_Win_ChangesOwner_SetsCaptureStability_AndAwards()
        {
            var state = new GameState { PvpAttacksRemaining = 1 };
            var mapCfg = CreateSmallMapConfig(botMult: 0.01f);
            var map = new MapService(state.MapState, mapCfg);
            map.AdvanceTime(nowUnixSeconds: 1000);

            var economy = CreateEconomyWithState(state);
            var balance = CreateValidBalanceConfig();
            var production = new ProductionService(state, balance, economy);
            var pvp = CreateDeterministicPvpConfig(prestigePowerPerPrestige: 99);
            state.PrestigeCount = 1; // attacker PvpPower = 100

            var snapshot = new SnapshotService(state, pvp, production, mapConfig: mapCfg);
            var league = new LeagueService(state, CreateValidLeagueConfig());
            var sim = new BattleSimService(pvp);
            var matchmaking = new MatchmakingService(mapCfg);
            var attacks = new PvpAttackChargesService(state, CreateDefaultPvpAttacksConfig());
            var combat = new PvpMapCombatService(state, map, mapCfg, attacks, snapshot, matchmaking, sim, economy, league);

            var now = 1001L;
            var result = combat.AttackSector(sectorId: 1, strategy: AttackStrategy.Aggressive, nowUnixSeconds: now, seed: 7);

            Assert.IsTrue(result.Win);
            Assert.AreEqual(0, state.PvpAttacksRemaining);
            Assert.AreEqual(mapCfg.WinSeasonPoints, result.Battle.LeaguePointsDelta);
            Assert.AreEqual(mapCfg.WinSoftReward, result.Battle.SoftReward);
            Assert.AreEqual(mapCfg.WinSoftReward, economy.Balance);

            var sector = map.GetSector(1);
            Assert.AreEqual(1, sector.OwnerPlayerId);
            Assert.AreEqual(now, sector.CapturedUnixSeconds);
            Assert.AreEqual(now, sector.LastCombatUnixSeconds);
            Assert.AreEqual(mapCfg.GetCaptureStabilityStart(AttackStrategy.Aggressive), sector.Stability);
        }

        [Test]
        public void AttackSector_Lose_DoesNotChangeOwner_IncreasesStability_AndAwardsConsolation()
        {
            var state = new GameState { PvpAttacksRemaining = 1 };
            var mapCfg = CreateSmallMapConfig(botMult: 100.0f);
            var map = new MapService(state.MapState, mapCfg);
            map.AdvanceTime(nowUnixSeconds: 1000);

            var economy = CreateEconomyWithState(state);
            var balance = CreateValidBalanceConfig();
            var production = new ProductionService(state, balance, economy);
            var pvp = CreateDeterministicPvpConfig(prestigePowerPerPrestige: 99);
            state.PrestigeCount = 1; // attacker PvpPower = 100

            var snapshot = new SnapshotService(state, pvp, production, mapConfig: mapCfg);
            var league = new LeagueService(state, CreateValidLeagueConfig());
            var sim = new BattleSimService(pvp);
            var matchmaking = new MatchmakingService(mapCfg);
            var attacks = new PvpAttackChargesService(state, CreateDefaultPvpAttacksConfig());
            var combat = new PvpMapCombatService(state, map, mapCfg, attacks, snapshot, matchmaking, sim, economy, league);

            var sectorBefore = map.GetSector(1);
            var stabilityBefore = sectorBefore.Stability;

            var now = 1001L;
            var result = combat.AttackSector(sectorId: 1, strategy: AttackStrategy.Stable, nowUnixSeconds: now, seed: 7);

            Assert.IsFalse(result.Win);
            Assert.AreEqual(0, state.PvpAttacksRemaining);
            Assert.AreEqual(mapCfg.LoseSeasonPoints, result.Battle.LeaguePointsDelta);
            Assert.AreEqual(mapCfg.LoseSoftReward, result.Battle.SoftReward);
            Assert.AreEqual(mapCfg.LoseSoftReward, economy.Balance);

            var sector = map.GetSector(1);
            Assert.AreEqual(0, sector.OwnerPlayerId);
            Assert.AreEqual(now, sector.LastCombatUnixSeconds);
            Assert.Greater(sector.Stability, stabilityBefore);
        }

        [Test]
        public void GetAttackPreview_ReturnsBoundedChanceRange()
        {
            var state = new GameState { PvpAttacksRemaining = 1 };
            var mapCfg = CreateSmallMapConfig(botMult: 1.0f);
            var map = new MapService(state.MapState, mapCfg);
            map.AdvanceTime(nowUnixSeconds: 1000);

            var economy = CreateEconomyWithState(state);
            var balance = CreateValidBalanceConfig();
            var production = new ProductionService(state, balance, economy);
            var pvp = CreateDeterministicPvpConfig(prestigePowerPerPrestige: 99);
            state.PrestigeCount = 1;

            var snapshot = new SnapshotService(state, pvp, production, mapConfig: mapCfg);
            var league = new LeagueService(state, CreateValidLeagueConfig());
            var sim = new BattleSimService(pvp);
            var matchmaking = new MatchmakingService(mapCfg);
            var attacks = new PvpAttackChargesService(state, CreateDefaultPvpAttacksConfig());
            var combat = new PvpMapCombatService(state, map, mapCfg, attacks, snapshot, matchmaking, sim, economy, league);

            var preview = combat.GetAttackPreview(sectorId: 1, strategy: AttackStrategy.Stable, nowUnixSeconds: 1001, seedBase: 123);

            Assert.AreEqual(1, preview.SectorId);
            Assert.AreEqual(AttackStrategy.Stable, preview.Strategy);
            Assert.GreaterOrEqual(preview.WinChanceMin, 0f);
            Assert.LessOrEqual(preview.WinChanceMax, 1f);
            Assert.LessOrEqual(preview.WinChanceMin, preview.WinChanceMax);
            Assert.AreEqual(preview.WinChanceMin, preview.WinChanceMax, 1e-6f);
        }

        [Test]
        public void GetAttackPreview_FlankBonus_IncreasesWinChance()
        {
            var state = new GameState { PvpAttacksRemaining = 1, PrestigeCount = 1 };
            state.MapState.Sectors = new[]
            {
                new SectorState { SectorId = 0, OwnerPlayerId = 1, Stability = 100f },
                new SectorState { SectorId = 1, OwnerPlayerId = 1, Stability = 100f },
                new SectorState { SectorId = 2, OwnerPlayerId = 2, Stability = 0f },
            };

            var mapCfg = ScriptableObject.CreateInstance<MapConfig>();
            mapCfg.MapSeasonLengthDays = 7;
            mapCfg.MapSeasonAnchorUnixSecondsUtc = 0;
            mapCfg.LocalPlayerId = 1;
            mapCfg.HomeSectorId = 0;
            mapCfg.HomeSectorStability = 100f;
            mapCfg.EnforceSectorCountRange = false;
            mapCfg.RequireConnectedGraph = true;
            mapCfg.SectorDefinitions = new[]
            {
                new MapConfig.SectorDefinition { SectorId = 0, Name = "Home" },
                new MapConfig.SectorDefinition { SectorId = 1, Name = "Flank" },
                new MapConfig.SectorDefinition { SectorId = 2, Name = "Target" },
            };
            mapCfg.Adjacency = new[]
            {
                new MapConfig.SectorEdge { A = 0, B = 2 },
                new MapConfig.SectorEdge { A = 1, B = 2 },
                new MapConfig.SectorEdge { A = 0, B = 1 },
            };
            mapCfg.StabilityGrowthPerSecond = 0f;
            mapCfg.FreshCaptureWindowSeconds = 60;
            mapCfg.StabilityStartNeutral = 0f;
            mapCfg.StabilityStartOnCapture = 10f;
            mapCfg.StabilityGainOnDefenseWin = 5f;
            mapCfg.CaptureStabilityMultiplierAggressive = 1f;
            mapCfg.CaptureStabilityMultiplierStable = 1f;
            mapCfg.CaptureStabilityMultiplierRisky = 1f;
            mapCfg.BotPowerMinMultiplier = 1f;
            mapCfg.BotPowerMaxMultiplier = 1f;
            mapCfg.ValidateOrThrow();

            var economy = new EconomyService(state);
            var balance = CreateValidBalanceConfig();
            var production = new ProductionService(state, balance, economy);
            var withoutFlank = CreateDeterministicPvpConfig(prestigePowerPerPrestige: 99);
            withoutFlank.FlankBonusPerExtraAttacker = 0f;
            withoutFlank.FlankBonusMaxMultiplier = 1f;
            withoutFlank.ValidateOrThrow();

            var withFlank = CreateDeterministicPvpConfig(prestigePowerPerPrestige: 99);
            withFlank.FlankBonusPerExtraAttacker = 0.25f;
            withFlank.FlankBonusMaxMultiplier = 2f;
            withFlank.ValidateOrThrow();

            var map = new MapService(state.MapState, mapCfg);
            map.AdvanceTime(1000);

            var snapshotNoFlank = new SnapshotService(state, withoutFlank, production, mapConfig: mapCfg);
            var snapshotWithFlank = new SnapshotService(state, withFlank, production, mapConfig: mapCfg);
            var matchmaking = new MatchmakingService(mapCfg);
            var attacks = new PvpAttackChargesService(state, CreateDefaultPvpAttacksConfig());
            var league = new LeagueService(state, CreateValidLeagueConfig());

            var combatNoFlank = new PvpMapCombatService(state, map, mapCfg, attacks, snapshotNoFlank, matchmaking, new BattleSimService(withoutFlank), economy, league);
            var combatWithFlank = new PvpMapCombatService(state, map, mapCfg, attacks, snapshotWithFlank, matchmaking, new BattleSimService(withFlank), economy, league);

            var previewNoFlank = combatNoFlank.GetAttackPreview(2, AttackStrategy.Stable, 1001, seedBase: 1);
            var previewWithFlank = combatWithFlank.GetAttackPreview(2, AttackStrategy.Stable, 1001, seedBase: 1);

            Assert.Greater(previewWithFlank.WinChanceMin, previewNoFlank.WinChanceMin);
        }

        [Test]
        public void GetAttackPreview_MaintenancePenalty_ReducesWinChance_ForExpandedAttacker()
        {
            var state = new GameState { PvpAttacksRemaining = 1, PrestigeCount = 1 };
            state.MapState.Sectors = new[]
            {
                new SectorState { SectorId = 0, OwnerPlayerId = 1, Stability = 100f },
                new SectorState { SectorId = 1, OwnerPlayerId = 1, Stability = 100f },
                new SectorState { SectorId = 2, OwnerPlayerId = 1, Stability = 100f },
                new SectorState { SectorId = 3, OwnerPlayerId = 2, Stability = 0f },
            };

            var mapCfg = ScriptableObject.CreateInstance<MapConfig>();
            mapCfg.MapSeasonLengthDays = 7;
            mapCfg.MapSeasonAnchorUnixSecondsUtc = 0;
            mapCfg.LocalPlayerId = 1;
            mapCfg.HomeSectorId = 0;
            mapCfg.HomeSectorStability = 100f;
            mapCfg.EnforceSectorCountRange = false;
            mapCfg.RequireConnectedGraph = true;
            mapCfg.SectorDefinitions = new[]
            {
                new MapConfig.SectorDefinition { SectorId = 0, Name = "Home" },
                new MapConfig.SectorDefinition { SectorId = 1, Name = "A" },
                new MapConfig.SectorDefinition { SectorId = 2, Name = "B" },
                new MapConfig.SectorDefinition { SectorId = 3, Name = "Target" },
            };
            mapCfg.Adjacency = new[]
            {
                new MapConfig.SectorEdge { A = 0, B = 1 },
                new MapConfig.SectorEdge { A = 1, B = 2 },
                new MapConfig.SectorEdge { A = 2, B = 3 },
            };
            mapCfg.StabilityGrowthPerSecond = 0f;
            mapCfg.FreshCaptureWindowSeconds = 60;
            mapCfg.StabilityStartNeutral = 0f;
            mapCfg.StabilityStartOnCapture = 10f;
            mapCfg.StabilityGainOnDefenseWin = 5f;
            mapCfg.CaptureStabilityMultiplierAggressive = 1f;
            mapCfg.CaptureStabilityMultiplierStable = 1f;
            mapCfg.CaptureStabilityMultiplierRisky = 1f;
            mapCfg.BotPowerMinMultiplier = 1f;
            mapCfg.BotPowerMaxMultiplier = 1f;
            mapCfg.ValidateOrThrow();

            var economy = new EconomyService(state);
            var balance = CreateValidBalanceConfig();
            var production = new ProductionService(state, balance, economy);
            var noMaintenance = CreateDeterministicPvpConfig(prestigePowerPerPrestige: 99);
            noMaintenance.CombatMaintenanceFreeSectors = 999;
            noMaintenance.CombatMaintenancePenaltyPerExtraSector = 0f;
            noMaintenance.CombatMaintenanceMinMultiplier = 1f;
            noMaintenance.ValidateOrThrow();

            var withMaintenance = CreateDeterministicPvpConfig(prestigePowerPerPrestige: 99);
            withMaintenance.CombatMaintenanceFreeSectors = 1;
            withMaintenance.CombatMaintenancePenaltyPerExtraSector = 0.2f;
            withMaintenance.CombatMaintenanceMinMultiplier = 0.5f;
            withMaintenance.ValidateOrThrow();

            var map = new MapService(state.MapState, mapCfg);
            map.AdvanceTime(1000);

            var snapshotNoMaintenance = new SnapshotService(state, noMaintenance, production, mapConfig: mapCfg);
            var snapshotWithMaintenance = new SnapshotService(state, withMaintenance, production, mapConfig: mapCfg);
            var matchmaking = new MatchmakingService(mapCfg);
            var attacks = new PvpAttackChargesService(state, CreateDefaultPvpAttacksConfig());
            var league = new LeagueService(state, CreateValidLeagueConfig());

            var combatNoMaintenance = new PvpMapCombatService(state, map, mapCfg, attacks, snapshotNoMaintenance, matchmaking, new BattleSimService(noMaintenance), economy, league);
            var combatWithMaintenance = new PvpMapCombatService(state, map, mapCfg, attacks, snapshotWithMaintenance, matchmaking, new BattleSimService(withMaintenance), economy, league);

            var previewNoMaintenance = combatNoMaintenance.GetAttackPreview(3, AttackStrategy.Stable, 1001, seedBase: 1);
            var previewWithMaintenance = combatWithMaintenance.GetAttackPreview(3, AttackStrategy.Stable, 1001, seedBase: 1);

            Assert.Less(previewWithMaintenance.WinChanceMin, previewNoMaintenance.WinChanceMin);
        }

        [Test]
        public void GetAttackPreview_UnderdogBonus_IncreasesWinChance_WhenDefenderHasMoreSectors()
        {
            var state = new GameState { PvpAttacksRemaining = 1, PrestigeCount = 1 };
            state.MapState.Sectors = new[]
            {
                new SectorState { SectorId = 0, OwnerPlayerId = 1, Stability = 100f },
                new SectorState { SectorId = 1, OwnerPlayerId = 2, Stability = 0f },
                new SectorState { SectorId = 2, OwnerPlayerId = 2, Stability = 100f },
            };

            var mapCfg = ScriptableObject.CreateInstance<MapConfig>();
            mapCfg.MapSeasonLengthDays = 7;
            mapCfg.MapSeasonAnchorUnixSecondsUtc = 0;
            mapCfg.LocalPlayerId = 1;
            mapCfg.HomeSectorId = 0;
            mapCfg.HomeSectorIds = new[] { 0, 2 };
            mapCfg.HomeSectorOwnerPlayerIds = new[] { 1, 2 };
            mapCfg.HomeSectorStability = 100f;
            mapCfg.EnforceSectorCountRange = false;
            mapCfg.RequireConnectedGraph = true;
            mapCfg.SectorDefinitions = new[]
            {
                new MapConfig.SectorDefinition { SectorId = 0, Name = "PlayerHome" },
                new MapConfig.SectorDefinition { SectorId = 1, Name = "Frontier" },
                new MapConfig.SectorDefinition { SectorId = 2, Name = "BotHome" },
            };
            mapCfg.Adjacency = new[]
            {
                new MapConfig.SectorEdge { A = 0, B = 1 },
                new MapConfig.SectorEdge { A = 1, B = 2 },
            };
            mapCfg.StabilityGrowthPerSecond = 0f;
            mapCfg.FreshCaptureWindowSeconds = 60;
            mapCfg.StabilityStartNeutral = 0f;
            mapCfg.StabilityStartOnCapture = 10f;
            mapCfg.StabilityGainOnDefenseWin = 5f;
            mapCfg.CaptureStabilityMultiplierAggressive = 1f;
            mapCfg.CaptureStabilityMultiplierStable = 1f;
            mapCfg.CaptureStabilityMultiplierRisky = 1f;
            mapCfg.BotPowerMinMultiplier = 1f;
            mapCfg.BotPowerMaxMultiplier = 1f;
            mapCfg.ValidateOrThrow();

            var economy = new EconomyService(state);
            var balance = CreateValidBalanceConfig();
            var production = new ProductionService(state, balance, economy);
            var noUnderdog = CreateDeterministicPvpConfig(prestigePowerPerPrestige: 99);
            noUnderdog.UnderdogMaxAttackBonus = 0f;
            noUnderdog.UnderdogSectorDeficitForMaxBonus = 1;
            noUnderdog.ValidateOrThrow();

            var withUnderdog = CreateDeterministicPvpConfig(prestigePowerPerPrestige: 99);
            withUnderdog.UnderdogMaxAttackBonus = 0.5f;
            withUnderdog.UnderdogSectorDeficitForMaxBonus = 2;
            withUnderdog.ValidateOrThrow();

            var map = new MapService(state.MapState, mapCfg);
            map.AdvanceTime(1000);

            var snapshotNoUnderdog = new SnapshotService(state, noUnderdog, production, mapConfig: mapCfg);
            var snapshotWithUnderdog = new SnapshotService(state, withUnderdog, production, mapConfig: mapCfg);
            var matchmaking = new MatchmakingService(mapCfg);
            var attacks = new PvpAttackChargesService(state, CreateDefaultPvpAttacksConfig());
            var league = new LeagueService(state, CreateValidLeagueConfig());

            var combatNoUnderdog = new PvpMapCombatService(state, map, mapCfg, attacks, snapshotNoUnderdog, matchmaking, new BattleSimService(noUnderdog), economy, league);
            var combatWithUnderdog = new PvpMapCombatService(state, map, mapCfg, attacks, snapshotWithUnderdog, matchmaking, new BattleSimService(withUnderdog), economy, league);

            var previewNoUnderdog = combatNoUnderdog.GetAttackPreview(1, AttackStrategy.Stable, 1001, seedBase: 1);
            var previewWithUnderdog = combatWithUnderdog.GetAttackPreview(1, AttackStrategy.Stable, 1001, seedBase: 1);

            Assert.Greater(previewWithUnderdog.WinChanceMin, previewNoUnderdog.WinChanceMin);
        }

        [Test]
        public void NeutralSector_Uses_Weaker_DefenderSnapshot_Than_BotSector()
        {
            var mapCfg = CreateSmallMapConfig(botMult: 1.0f);
            mapCfg.NeutralPowerMinMultiplier = 0.5f;
            mapCfg.NeutralPowerMaxMultiplier = 0.5f;
            mapCfg.ValidateOrThrow();

            var matchmaking = new MatchmakingService(mapCfg);
            var attacker = new PvpSnapshot { PvpPower = 100 };

            var neutralSector = new SectorState { SectorId = 1, OwnerPlayerId = 0, Stability = 0f };
            var botSector = new SectorState { SectorId = 2, OwnerPlayerId = 2, Stability = 0f };

            var neutral = matchmaking.GetDefenderSnapshot(attacker, neutralSector, seed: 1);
            var bot = matchmaking.GetDefenderSnapshot(attacker, botSector, seed: 1);

            Assert.AreEqual(50d, neutral.PvpPower, 1e-9);
            Assert.AreEqual(100d, bot.PvpPower, 1e-9);
        }

        [Test]
        public void BotService_Uses_AttackCharges_And_DoesNotAttackAgain_WithoutRegen()
        {
            var state = new GameState { PrestigeCount = 1 };
            state.MapState.Sectors = new[]
            {
                new SectorState { SectorId = 0, OwnerPlayerId = 1, Stability = 100f },
                new SectorState { SectorId = 1, OwnerPlayerId = 0, Stability = 0f },
                new SectorState { SectorId = 2, OwnerPlayerId = 3, Stability = 100f },
                new SectorState { SectorId = 3, OwnerPlayerId = 0, Stability = 0f },
            };

            var mapCfg = ScriptableObject.CreateInstance<MapConfig>();
            mapCfg.MapSeasonLengthDays = 7;
            mapCfg.MapSeasonAnchorUnixSecondsUtc = 0;
            mapCfg.LocalPlayerId = 1;
            mapCfg.HomeSectorId = 0;
            mapCfg.HomeSectorIds = new[] { 0, 2 };
            mapCfg.HomeSectorOwnerPlayerIds = new[] { 1, 3 };
            mapCfg.HomeSectorStability = 100f;
            mapCfg.EnforceSectorCountRange = false;
            mapCfg.RequireConnectedGraph = true;
            mapCfg.SectorDefinitions = new[]
            {
                new MapConfig.SectorDefinition { SectorId = 0, Name = "PlayerHome" },
                new MapConfig.SectorDefinition { SectorId = 1, Name = "N1" },
                new MapConfig.SectorDefinition { SectorId = 2, Name = "BotHome" },
                new MapConfig.SectorDefinition { SectorId = 3, Name = "N2" },
            };
            mapCfg.Adjacency = new[]
            {
                new MapConfig.SectorEdge { A = 2, B = 1 },
                new MapConfig.SectorEdge { A = 2, B = 3 },
                new MapConfig.SectorEdge { A = 0, B = 1 },
            };
            mapCfg.StabilityGrowthPerSecond = 0f;
            mapCfg.FreshCaptureWindowSeconds = 60;
            mapCfg.StabilityStartNeutral = 0f;
            mapCfg.StabilityStartOnCapture = 10f;
            mapCfg.StabilityGainOnDefenseWin = 5f;
            mapCfg.CaptureStabilityMultiplierAggressive = 1f;
            mapCfg.CaptureStabilityMultiplierStable = 1f;
            mapCfg.CaptureStabilityMultiplierRisky = 1f;
            mapCfg.BotPowerMinMultiplier = 0.5f;
            mapCfg.BotPowerMaxMultiplier = 0.5f;
            mapCfg.NeutralPowerMinMultiplier = 0.5f;
            mapCfg.NeutralPowerMaxMultiplier = 0.5f;
            mapCfg.ValidateOrThrow();

            var economy = new EconomyService(state);
            var balance = CreateValidBalanceConfig();
            var production = new ProductionService(state, balance, economy);
            var pvp = CreateDeterministicPvpConfig(prestigePowerPerPrestige: 99);
            var snapshot = new SnapshotService(state, pvp, production, mapConfig: mapCfg);
            var matchmaking = new MatchmakingService(mapCfg);
            var sim = new BattleSimService(pvp);
            var attacksCfg = CreateDefaultPvpAttacksConfig(maxAttacks: 1, regenSeconds: 10_000);

            var map = new MapService(state.MapState, mapCfg);
            map.AdvanceTime(1_000);

            var bots = new PvpBotService(state, map, mapCfg, snapshot, matchmaking, sim, attacksCfg);

            bots.Tick(300);
            var ownedAfterFirst = 0;
            for (var i = 0; i < state.MapState.Sectors.Length; i++)
            {
                if (state.MapState.Sectors[i].OwnerPlayerId == 3) ownedAfterFirst++;
            }

            bots.Tick(600);
            var ownedAfterSecond = 0;
            for (var i = 0; i < state.MapState.Sectors.Length; i++)
            {
                if (state.MapState.Sectors[i].OwnerPlayerId == 3) ownedAfterSecond++;
            }

            Assert.AreEqual(2, ownedAfterFirst);
            Assert.AreEqual(ownedAfterFirst, ownedAfterSecond);
        }

        [Test]
        public void DebugResetMatch_RestoresPlayerAndBotAttackChargesToMax()
        {
            var state = new GameState { PvpAttacksRemaining = 0, PrestigeCount = 1 };
            state.MapState.Sectors = new[]
            {
                new SectorState { SectorId = 0, OwnerPlayerId = 1, Stability = 100f },
                new SectorState { SectorId = 1, OwnerPlayerId = 0, Stability = 0f },
                new SectorState { SectorId = 2, OwnerPlayerId = 3, Stability = 100f },
                new SectorState { SectorId = 3, OwnerPlayerId = 0, Stability = 0f },
            };

            var mapCfg = ScriptableObject.CreateInstance<MapConfig>();
            mapCfg.MapSeasonLengthDays = 7;
            mapCfg.MapSeasonAnchorUnixSecondsUtc = 0;
            mapCfg.LocalPlayerId = 1;
            mapCfg.HomeSectorId = 0;
            mapCfg.HomeSectorIds = new[] { 0, 2 };
            mapCfg.HomeSectorOwnerPlayerIds = new[] { 1, 3 };
            mapCfg.HomeSectorStability = 100f;
            mapCfg.EnforceSectorCountRange = false;
            mapCfg.RequireConnectedGraph = true;
            mapCfg.SectorDefinitions = new[]
            {
                new MapConfig.SectorDefinition { SectorId = 0, Name = "PlayerHome" },
                new MapConfig.SectorDefinition { SectorId = 1, Name = "N1" },
                new MapConfig.SectorDefinition { SectorId = 2, Name = "BotHome" },
                new MapConfig.SectorDefinition { SectorId = 3, Name = "N2" },
            };
            mapCfg.Adjacency = new[]
            {
                new MapConfig.SectorEdge { A = 2, B = 1 },
                new MapConfig.SectorEdge { A = 2, B = 3 },
                new MapConfig.SectorEdge { A = 0, B = 1 },
            };
            mapCfg.StabilityGrowthPerSecond = 0f;
            mapCfg.FreshCaptureWindowSeconds = 60;
            mapCfg.StabilityStartNeutral = 0f;
            mapCfg.StabilityStartOnCapture = 10f;
            mapCfg.StabilityGainOnDefenseWin = 5f;
            mapCfg.CaptureStabilityMultiplierAggressive = 1f;
            mapCfg.CaptureStabilityMultiplierStable = 1f;
            mapCfg.CaptureStabilityMultiplierRisky = 1f;
            mapCfg.BotPowerMinMultiplier = 0.5f;
            mapCfg.BotPowerMaxMultiplier = 0.5f;
            mapCfg.NeutralPowerMinMultiplier = 0.5f;
            mapCfg.NeutralPowerMaxMultiplier = 0.5f;
            mapCfg.ValidateOrThrow();

            var economy = new EconomyService(state);
            var balance = CreateValidBalanceConfig();
            var production = new ProductionService(state, balance, economy);
            var pvp = CreateDeterministicPvpConfig(prestigePowerPerPrestige: 99);
            var snapshot = new SnapshotService(state, pvp, production, mapConfig: mapCfg);
            var matchmaking = new MatchmakingService(mapCfg);
            var sim = new BattleSimService(pvp);
            var attacksCfg = CreateDefaultPvpAttacksConfig(maxAttacks: 2, regenSeconds: 10_000);

            var map = new MapService(state.MapState, mapCfg);
            map.AdvanceTime(1_000);

            var playerAttacks = new PvpAttackChargesService(state, attacksCfg);
            var bots = new PvpBotService(state, map, mapCfg, snapshot, matchmaking, sim, attacksCfg);

            bots.Tick(300);
            bots.Tick(600);

            playerAttacks.DebugResetToMax(700);
            bots.DebugResetMatch(700);

            Assert.AreEqual(2, state.PvpAttacksRemaining);

            bots.Tick(600);
            var ownedByBotAfterSameBucket = 0;
            for (var i = 0; i < state.MapState.Sectors.Length; i++)
            {
                if (state.MapState.Sectors[i].OwnerPlayerId == 3) ownedByBotAfterSameBucket++;
            }

            Assert.Greater(ownedByBotAfterSameBucket, 2);
        }

        [Test]
        public void ProductionService_Applies_SectorBonus_And_Maintenance_Via_MapProductionBonusProvider()
        {
            var state = new GameState();
            var mapCfg = CreateSmallMapConfig(botMult: 1.0f);
            mapCfg.MaintenanceFreeSectors = 1;
            mapCfg.MaintenancePenaltyPercentPerExtraSector = 10f;
            mapCfg.SectorBonusCapPercent = 0;
            mapCfg.ValidateOrThrow();

            state.MapState.Sectors = new[]
            {
                new SectorState { SectorId = 0, OwnerPlayerId = 1 },
                new SectorState { SectorId = 1, OwnerPlayerId = 1 },
                new SectorState { SectorId = 2, OwnerPlayerId = 1 },
            };

            var provider = new MapProductionBonusProvider(state.MapState, mapCfg);

            var economy = new EconomyService(state);
            var balance = CreateValidBalanceConfig();
            var production = new ProductionService(state, balance, economy, permanentMultiplierProvider: provider);

            state.GeneratorLevels[0] = 1;
            var pps = production.CalculateProductionPerSecond();

            // Raw bonus: 3 sectors * 5% = 15%. Tier multiplier for 3 sectors is 1.0 => 15%.
            // Maintenance: free=1, extra=2, penalty=20% => maintenance multiplier 0.8.
            // Total multiplier: 1.15 * 0.8 = 0.92. Base PPS is 100.
            Assert.AreEqual(92.0, pps, 1e-9);
        }

        [Test]
        public void ProductionService_Ignores_Island_Sectors_NotConnected_To_Home()
        {
            var state = new GameState();
            var mapCfg = ScriptableObject.CreateInstance<MapConfig>();
            mapCfg.MapSeasonLengthDays = 7;
            mapCfg.MapSeasonAnchorUnixSecondsUtc = 0;
            mapCfg.LocalPlayerId = 1;
            mapCfg.HomeSectorId = 0;
            mapCfg.HomeSectorIds = new[] { 0 };
            mapCfg.HomeSectorOwnerPlayerIds = new[] { 1 };
            mapCfg.HomeSectorStability = 100f;
            mapCfg.EnforceSectorCountRange = false;
            mapCfg.RequireConnectedGraph = false;
            mapCfg.SectorDefinitions = new[]
            {
                new MapConfig.SectorDefinition { SectorId = 0, Name = "Home", ProductionBonusPercent = 5f },
                new MapConfig.SectorDefinition { SectorId = 1, Name = "Front", ProductionBonusPercent = 5f },
                new MapConfig.SectorDefinition { SectorId = 2, Name = "Bridge", ProductionBonusPercent = 5f },
                new MapConfig.SectorDefinition { SectorId = 3, Name = "Island", ProductionBonusPercent = 5f },
            };
            mapCfg.Adjacency = new[]
            {
                new MapConfig.SectorEdge { A = 0, B = 1 },
                new MapConfig.SectorEdge { A = 1, B = 2 },
                new MapConfig.SectorEdge { A = 2, B = 3 },
            };
            mapCfg.MaintenanceFreeSectors = 1;
            mapCfg.MaintenancePenaltyPercentPerExtraSector = 10f;
            mapCfg.SectorBonusCapPercent = 0f;
            mapCfg.ValidateOrThrow();

            state.MapState.Sectors = new[]
            {
                new SectorState { SectorId = 0, OwnerPlayerId = 1 },
                new SectorState { SectorId = 1, OwnerPlayerId = 0 },
                new SectorState { SectorId = 2, OwnerPlayerId = 0 },
                new SectorState { SectorId = 3, OwnerPlayerId = 1 },
            };

            var provider = new MapProductionBonusProvider(state.MapState, mapCfg);
            var economy = new EconomyService(state);
            var balance = CreateValidBalanceConfig();
            var production = new ProductionService(state, balance, economy, permanentMultiplierProvider: provider);

            state.GeneratorLevels[0] = 1;
            var pps = production.CalculateProductionPerSecond();

            Assert.AreEqual(105.0, pps, 1e-9);
        }

        [Test]
        public void Snapshot_Ignores_Island_Sectors_NotConnected_To_Home()
        {
            var state = new GameState();
            var balance = CreateValidBalanceConfig();
            var economy = new EconomyService(state);
            var production = new ProductionService(state, balance, economy);
            var pvp = CreateDeterministicPvpConfig(prestigePowerPerPrestige: 0.0);
            pvp.PermanentPvpPowerPerLevel = 0.0;
            pvp.SectorPvpPowerPerSector = 10.0;
            pvp.ProdToPvpMaxBonus = 0.0;
            pvp.ProdToPvpHalfCapPps = 1.0;
            pvp.ValidateOrThrow();

            var mapCfg = ScriptableObject.CreateInstance<MapConfig>();
            mapCfg.MapSeasonLengthDays = 7;
            mapCfg.MapSeasonAnchorUnixSecondsUtc = 0;
            mapCfg.LocalPlayerId = 1;
            mapCfg.HomeSectorId = 0;
            mapCfg.HomeSectorIds = new[] { 0 };
            mapCfg.HomeSectorOwnerPlayerIds = new[] { 1 };
            mapCfg.HomeSectorStability = 100f;
            mapCfg.EnforceSectorCountRange = false;
            mapCfg.RequireConnectedGraph = false;
            mapCfg.SectorDefinitions = new[]
            {
                new MapConfig.SectorDefinition { SectorId = 0, Name = "Home", PvpPowerBonusFlat = 2.0, PvpPowerBonusPercent = 0f },
                new MapConfig.SectorDefinition { SectorId = 1, Name = "Bridge", PvpPowerBonusFlat = 3.0, PvpPowerBonusPercent = 0f },
                new MapConfig.SectorDefinition { SectorId = 2, Name = "Island", PvpPowerBonusFlat = 100.0, PvpPowerBonusPercent = 0f },
            };
            mapCfg.Adjacency = new[]
            {
                new MapConfig.SectorEdge { A = 0, B = 1 },
                new MapConfig.SectorEdge { A = 1, B = 2 },
            };
            mapCfg.ValidateOrThrow();

            state.MapState.Sectors = new[]
            {
                new SectorState { SectorId = 0, OwnerPlayerId = 1 },
                new SectorState { SectorId = 1, OwnerPlayerId = 0 },
                new SectorState { SectorId = 2, OwnerPlayerId = 1 },
            };

            var snapshot = new SnapshotService(state, pvp, production, mapConfig: mapCfg);
            var result = snapshot.BuildSnapshot();

            Assert.AreEqual(13.0, result.PvpPower, 1e-9);
        }

        [Test]
        public void MatchmakingService_BotSnapshot_IsWithinConfiguredRange()
        {
            var cfg = CreateSmallMapConfig(botMult: 1.0f);
            cfg.BotPowerMinMultiplier = 0.8f;
            cfg.BotPowerMaxMultiplier = 1.2f;
            cfg.ValidateOrThrow();

            var mm = new MatchmakingService(cfg);
            var attacker = new PvpSnapshot { PvpPower = 100, League = 1, SeasonPoints = 50 };
            var sector = new SectorState { SectorId = 1, OwnerPlayerId = 0, OwnerSnapshot = new PvpSnapshot() };

            var bot = mm.GetDefenderSnapshot(attacker, sector, seed: 123);
            Assert.GreaterOrEqual(bot.PvpPower, 80.0);
            Assert.LessOrEqual(bot.PvpPower, 120.0);
        }

        [Test]
        public void MatchmakingService_OwnerSnapshot_ReturnsExactSnapshot()
        {
            var cfg = CreateSmallMapConfig(botMult: 1.0f);
            var mm = new MatchmakingService(cfg);
            var attacker = new PvpSnapshot { PvpPower = 100, League = 1, SeasonPoints = 50 };
            var owner = new PvpSnapshot { PvpPower = 321.5, League = 2, SeasonPoints = 777 };
            var sector = new SectorState { SectorId = 1, OwnerPlayerId = 9, OwnerSnapshot = owner };

            var defender = mm.GetDefenderSnapshot(attacker, sector, seed: 1);
            Assert.AreEqual(owner.PvpPower, defender.PvpPower);
            Assert.AreEqual(owner.League, defender.League);
            Assert.AreEqual(owner.SeasonPoints, defender.SeasonPoints);
        }

        [Test]
        public void BattleSim_StrategyMultiplier_AffectsOutcome_Deterministically()
        {
            var pvp = ScriptableObject.CreateInstance<PvpConfig>();
            pvp.PowerVarianceMin = 1.0f;
            pvp.PowerVarianceMax = 1.0f;
            pvp.StrategyMultiplierAggressive = 1.2f;
            pvp.StrategyMultiplierStable = 1.0f;
            pvp.StrategyMultiplierRisky = 1.0f;
            pvp.StabilityMultiplierMin = 1.0f;
            pvp.StabilityMultiplierMax = 1.0f;
            pvp.PrestigePvpPowerPerPrestige = 0.0;
            pvp.PermanentPvpPowerPerLevel = 0.0;
            pvp.SectorPvpPowerPerSector = 0.0;
            pvp.ProdToPvpMaxBonus = 0.0;
            pvp.ProdToPvpHalfCapPps = 1.0;
            pvp.ValidateOrThrow();

            var sim = new BattleSimService(pvp);

            var stable = sim.Simulate(attackerPvpPower: 100, defenderPvpPower: 110, strategy: AttackStrategy.Stable, stability: 0f, seed: 1);
            var aggressive = sim.Simulate(attackerPvpPower: 100, defenderPvpPower: 110, strategy: AttackStrategy.Aggressive, stability: 0f, seed: 1);

            Assert.IsFalse(stable.Win);
            Assert.IsTrue(aggressive.Win);
        }

        [Test]
        public void BattleSim_StabilityMultiplier_IncreasesDefenseRoll()
        {
            var pvp = ScriptableObject.CreateInstance<PvpConfig>();
            pvp.PowerVarianceMin = 1.0f;
            pvp.PowerVarianceMax = 1.0f;
            pvp.StrategyMultiplierAggressive = 1.0f;
            pvp.StrategyMultiplierStable = 1.0f;
            pvp.StrategyMultiplierRisky = 1.0f;
            pvp.StabilityMultiplierMin = 1.0f;
            pvp.StabilityMultiplierMax = 2.0f;
            pvp.PrestigePvpPowerPerPrestige = 0.0;
            pvp.PermanentPvpPowerPerLevel = 0.0;
            pvp.SectorPvpPowerPerSector = 0.0;
            pvp.ProdToPvpMaxBonus = 0.0;
            pvp.ProdToPvpHalfCapPps = 1.0;
            pvp.ValidateOrThrow();

            var sim = new BattleSimService(pvp);

            var low = sim.Simulate(attackerPvpPower: 100, defenderPvpPower: 100, strategy: AttackStrategy.Stable, stability: 0f, seed: 1);
            var high = sim.Simulate(attackerPvpPower: 100, defenderPvpPower: 100, strategy: AttackStrategy.Stable, stability: 100f, seed: 1);

            Assert.Greater(high.DefenseRoll, low.DefenseRoll);
        }

        [Test]
        public void ProductionBonusProvider_NoSectors_Returns1()
        {
            var state = new GameState();
            var cfg = CreateSmallMapConfig(botMult: 1.0f);
            state.MapState.Sectors = new SectorState[0];

            var provider = new MapProductionBonusProvider(state.MapState, cfg);
            Assert.AreEqual(1.0, provider.GetPermanentMultiplier(), 1e-12);
        }

        [Test]
        public void ProductionBonusProvider_AppliesCap_AfterDiminishing()
        {
            var state = new GameState();
            var cfg = CreateSmallMapConfig(botMult: 1.0f);
            cfg.SectorBonusCapPercent = 10f;
            cfg.ValidateOrThrow();

            state.MapState.Sectors = new[]
            {
                new SectorState { SectorId = 0, OwnerPlayerId = 1 },
                new SectorState { SectorId = 1, OwnerPlayerId = 1 },
                new SectorState { SectorId = 2, OwnerPlayerId = 1 },
            };

            var provider = new MapProductionBonusProvider(state.MapState, cfg);
            // Raw bonus is 15%, tier multiplier is 1.0 => 15% => capped to 10%.
            Assert.AreEqual(1.10, provider.GetPermanentMultiplier(), 1e-12);
        }
    }
}
