using AIWarsIdle.GameCore.Config;
using AIWarsIdle.GameCore.Domain;
using AIWarsIdle.GameCore.Services;
using AIWarsIdle.PvP.Config;
using AIWarsIdle.PvP.Services;
using NUnit.Framework;
using UnityEngine;

namespace AIWarsIdle.Tests
{
    public sealed class Epic11PvpMapUiRuntimeTests
    {
        [Test]
        public void RuntimeMapFactory_Generates_Radius4_Map_With_Six_Homes_When_Config_Is_Empty()
        {
            var cfg = ScriptableObject.CreateInstance<MapConfig>();
            cfg.MapSeasonLengthDays = 7;
            cfg.LocalPlayerId = 1;
            cfg.HomeSectorId = 0;
            cfg.EnforceSectorCountRange = true;
            cfg.MinSectorCount = 20;
            cfg.MaxSectorCount = 30;
            cfg.SectorDefinitions = System.Array.Empty<MapConfig.SectorDefinition>();
            cfg.Adjacency = System.Array.Empty<MapConfig.SectorEdge>();

            var runtime = PvpRuntimeConfigFactory.CreateRuntimeMapConfig(cfg);

            Assert.AreEqual(61, runtime.SectorDefinitions.Length);
            Assert.AreEqual(6, runtime.GetEffectiveHomeSectorIds().Length);
            Assert.AreEqual(1, runtime.GetHomeOwnerPlayerId(runtime.GetEffectiveHomeSectorIds()[0]));
            Assert.AreEqual(6, runtime.GetHomeOwnerPlayerId(runtime.GetEffectiveHomeSectorIds()[5]));
        }

        [Test]
        public void CombatEvaluation_Returns_NoAdjacentOwnedSector_For_NonFrontier_Target()
        {
            var state = new GameState { PvpAttacksRemaining = 2 };

            var balance = ScriptableObject.CreateInstance<BalanceConfig>();
            balance.GeneratorBaseCosts = new[] { 10.0, 10.0, 10.0, 10.0, 10.0 };
            balance.GeneratorCostGrowthFactors = new[] { 1.15, 1.15, 1.15, 1.15, 1.15 };
            balance.GeneratorBaseOutputs = new[] { 1.0, 1.0, 1.0, 1.0, 1.0 };
            balance.OfflineCapSeconds = 60;

            var economy = new EconomyService(state);
            var production = new ProductionService(state, balance, economy);

            var pvp = ScriptableObject.CreateInstance<PvpConfig>();
            pvp.PowerVarianceMin = 1f;
            pvp.PowerVarianceMax = 1f;
            pvp.StrategyMultiplierAggressive = 1.05f;
            pvp.StrategyMultiplierStable = 1f;
            pvp.StrategyMultiplierRisky = 1.1f;
            pvp.StabilityMultiplierMin = 0.9f;
            pvp.StabilityMultiplierMax = 1.2f;
            pvp.PrestigePvpPowerPerPrestige = 1;
            pvp.PermanentPvpPowerPerLevel = 1;
            pvp.SectorPvpPowerPerSector = 1;
            pvp.ProdToPvpMaxBonus = 0;
            pvp.ProdToPvpHalfCapPps = 100;

            var attacksCfg = ScriptableObject.CreateInstance<PvpAttacksConfig>();
            attacksCfg.MaxAttacks = 3;
            attacksCfg.RegenSeconds = 60;
            attacksCfg.AdExtraAttacksPerDay = 1;
            attacksCfg.PremiumExtraAttacksPerPurchase = 1;
            attacksCfg.PremiumCurrencyCostPerPurchase = 1;

            var leagueCfg = ScriptableObject.CreateInstance<LeagueConfig>();
            leagueCfg.LeaguePointThresholds = new[] { 0, 100 };
            leagueCfg.SeasonMode = LeagueSeasonMode.FixedDays;
            leagueCfg.FixedSeasonLengthDays = 28;
            leagueCfg.FixedSeasonAnchorUnixSecondsUtc = 0;

            var mapCfg = ScriptableObject.CreateInstance<MapConfig>();
            mapCfg.LocalPlayerId = 1;
            mapCfg.HomeSectorId = 0;
            mapCfg.HomeSectorIds = new[] { 0 };
            mapCfg.HomeSectorOwnerPlayerIds = new[] { 1 };
            mapCfg.MapSeasonLengthDays = 3;
            mapCfg.HomeSectorStability = 100;
            mapCfg.EnforceSectorCountRange = false;
            mapCfg.RequireConnectedGraph = true;
            mapCfg.SectorDefinitions = new[]
            {
                new MapConfig.SectorDefinition { SectorId = 0, Name = "Home" },
                new MapConfig.SectorDefinition { SectorId = 1, Name = "Frontier" },
                new MapConfig.SectorDefinition { SectorId = 2, Name = "Backline" }
            };
            mapCfg.Adjacency = new[]
            {
                new MapConfig.SectorEdge { A = 0, B = 1 },
                new MapConfig.SectorEdge { A = 1, B = 2 }
            };

            var map = new MapService(state.MapState, mapCfg);
            map.AdvanceTime(1_000);

            var snapshot = new SnapshotService(state, pvp, production, mapCfg.LocalPlayerId, mapCfg);
            var matchmaking = new MatchmakingService(mapCfg);
            var sim = new BattleSimService(pvp);
            var attacks = new PvpAttackChargesService(state, attacksCfg);
            var league = new LeagueService(state, leagueCfg);
            var combat = new PvpMapCombatService(state, map, mapCfg, attacks, snapshot, matchmaking, sim, economy, league);

            var evaluation = combat.EvaluateAttack(2, 1_000);
            Assert.IsFalse(evaluation.CanAttack);
            Assert.AreEqual(PvpAttackBlockReason.NoAdjacentOwnedSector, evaluation.BlockReason);

            var frontier = combat.EvaluateAttack(1, 1_000);
            Assert.IsTrue(frontier.CanAttack);
        }
    }
}
