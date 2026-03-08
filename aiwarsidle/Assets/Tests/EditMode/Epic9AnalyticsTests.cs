using AIWarsIdle.Analytics;
using AIWarsIdle.GameCore.Config;
using AIWarsIdle.GameCore.Domain;
using AIWarsIdle.GameCore.Services;
using AIWarsIdle.Monetization;
using AIWarsIdle.PvP.Config;
using AIWarsIdle.PvP.Services;
using NUnit.Framework;
using UnityEngine;

namespace AIWarsIdle.Tests.EditMode
{
    public sealed class Epic9AnalyticsTests
    {
        [Test]
        public void BufferedAnalyticsService_Track_DoesNotThrow_OnNullOrEmptyParams()
        {
            var sink = new InMemoryAnalyticsSink();
            var analytics = new BufferedAnalyticsService(sink);

            Assert.DoesNotThrow(() => analytics.Track("evt"));
            Assert.DoesNotThrow(() => analytics.Track("evt", null));

            analytics.Flush(maxEvents: 10);
            Assert.GreaterOrEqual(sink.Events.Count, 2);
        }

        [Test]
        public void PvpAttack_Publishes_SectorAttack_And_SectorResult_Analytics()
        {
            var eventBus = new EventBus();
            var sink = new InMemoryAnalyticsSink();
            var analytics = new BufferedAnalyticsService(sink);
            using var bridge = new AnalyticsEventBusBridge(eventBus, analytics);

            var session = new SessionTelemetryService(eventBus);
            session.TrackSessionStart(nowUnixSeconds: 1000);

            var pvpTelemetry = new PvpTelemetryService(eventBus);
            pvpTelemetry.TrackMapOpen(nowUnixSeconds: 1000);
            pvpTelemetry.TrackSectorView(sectorId: 1);

            var subscription = new SubscriptionService(isActive: false, eventBus: eventBus);
            subscription.SetActive(true);

            var state = new GameState
            {
                PrestigeCount = 1,
                PvpAttacksRemaining = 1,
                LastPvpAttackRegenUnixSeconds = 0,
                NextPvpAttackRegenAtUnixSeconds = 1_000_000_000L
            };

            var balance = CreateValidBalanceConfig();
            var economy = new EconomyService(state);
            var production = new ProductionService(state, balance, economy);

            var pvpCfg = CreateDeterministicPvpConfig();
            var mapCfg = CreateSmallMapConfig();
            var leagueCfg = CreateValidLeagueConfig();
            var attacksCfg = CreatePvpAttacksConfig();

            var map = new MapService(state.MapState, mapCfg);
            map.AdvanceTime(nowUnixSeconds: 1000);

            var attacks = new PvpAttackChargesService(state, attacksCfg);
            var snapshot = new SnapshotService(state, pvpCfg, production, localPlayerId: mapCfg.LocalPlayerId, mapConfig: mapCfg);
            var mm = new MatchmakingService(mapCfg);
            var battle = new BattleSimService(pvpCfg);
            var league = new LeagueService(state, leagueCfg);

            var combat = new PvpMapCombatService(
                state,
                map,
                mapCfg,
                attacks,
                snapshot,
                mm,
                battle,
                economy,
                league,
                eventBus);

            var result = combat.AttackSector(sectorId: 1, strategy: AttackStrategy.Stable, nowUnixSeconds: 1000, seed: 123);
            Assert.NotNull(result);
            Assert.IsTrue(result.Win);

            analytics.Flush(maxEvents: 100);
            Assert.IsTrue(ContainsEvent(sink, "session_start"));
            Assert.IsTrue(ContainsEvent(sink, "subscription_started"));
            Assert.IsTrue(ContainsEvent(sink, "map_open"));
            Assert.IsTrue(ContainsEvent(sink, "sector_view"));
            Assert.IsTrue(ContainsEvent(sink, "sector_attack"));
            Assert.IsTrue(ContainsEvent(sink, "sector_result"));
            Assert.IsTrue(ContainsEvent(sink, "sector_ownership_changed"));
        }

        private static bool ContainsEvent(InMemoryAnalyticsSink sink, string name)
        {
            for (var i = 0; i < sink.Events.Count; i++)
            {
                if (sink.Events[i].Name == name) return true;
            }

            return false;
        }

        private static BalanceConfig CreateValidBalanceConfig()
        {
            var cfg = ScriptableObject.CreateInstance<BalanceConfig>();
            for (var i = 0; i < GameState.GeneratorCount; i++)
            {
                cfg.GeneratorBaseCosts[i] = 1;
                cfg.GeneratorCostGrowthFactors[i] = 1.2;
                cfg.GeneratorBaseOutputs[i] = 1;
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

        private static PvpConfig CreateDeterministicPvpConfig()
        {
            var cfg = ScriptableObject.CreateInstance<PvpConfig>();
            cfg.PowerVarianceMin = 1.0f;
            cfg.PowerVarianceMax = 1.0f;
            cfg.StrategyMultiplierAggressive = 1.0f;
            cfg.StrategyMultiplierStable = 1.0f;
            cfg.StrategyMultiplierRisky = 1.0f;
            cfg.StabilityMultiplierMin = 1.0f;
            cfg.StabilityMultiplierMax = 1.0f;

            cfg.PrestigePvpPowerPerPrestige = 99.0;
            cfg.PermanentPvpPowerPerLevel = 0.0;
            cfg.SectorPvpPowerPerSector = 0.0;
            cfg.ProdToPvpMaxBonus = 0.0;
            cfg.ProdToPvpHalfCapPps = 1.0;
            cfg.ValidateOrThrow();
            return cfg;
        }

        private static MapConfig CreateSmallMapConfig()
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
                new MapConfig.SectorDefinition { SectorId = 0, Name = "Home", ProductionBonusPercent = 0f },
                new MapConfig.SectorDefinition { SectorId = 1, Name = "A", ProductionBonusPercent = 0f },
            };

            cfg.Adjacency = new[]
            {
                new MapConfig.SectorEdge { A = 0, B = 1 },
            };

            cfg.StabilityGrowthPerSecond = 0f;
            cfg.FreshCaptureWindowSeconds = 0;
            cfg.StabilityStartNeutral = 0f;
            cfg.StabilityStartOnCapture = 10f;
            cfg.StabilityGainOnDefenseWin = 0f;
            cfg.CaptureStabilityMultiplierAggressive = 1.0f;
            cfg.CaptureStabilityMultiplierStable = 1.0f;
            cfg.CaptureStabilityMultiplierRisky = 1.0f;
            cfg.SectorAttackCooldownSeconds = 0;

            cfg.BotPowerMinMultiplier = 0.5f;
            cfg.BotPowerMaxMultiplier = 0.5f;
            cfg.NeutralPowerMinMultiplier = 0.01f;
            cfg.NeutralPowerMaxMultiplier = 0.01f;

            cfg.WinSeasonPoints = 0;
            cfg.LoseSeasonPoints = 0;
            cfg.WinSoftReward = 0;
            cfg.LoseSoftReward = 0;

            cfg.SectorBonusCapPercent = 0;
            cfg.MaintenanceFreeSectors = 5;
            cfg.MaintenancePenaltyPercentPerExtraSector = 1f;
            cfg.MaintenanceMinMultiplier = 0.1f;

            cfg.ValidateOrThrow();
            return cfg;
        }

        private static LeagueConfig CreateValidLeagueConfig()
        {
            var cfg = ScriptableObject.CreateInstance<LeagueConfig>();
            cfg.LeaguePointThresholds = new[] { 0, 100 };
            cfg.SeasonMode = LeagueSeasonMode.FixedDays;
            cfg.FixedSeasonLengthDays = 28;
            cfg.FixedSeasonAnchorUnixSecondsUtc = 0;
            cfg.ValidateOrThrow();
            return cfg;
        }

        private static PvpAttacksConfig CreatePvpAttacksConfig()
        {
            var cfg = ScriptableObject.CreateInstance<PvpAttacksConfig>();
            cfg.MaxAttacks = 5;
            cfg.RegenSeconds = 2 * 60 * 60;
            cfg.AdExtraAttacksPerDay = 1;
            cfg.PremiumExtraAttacksPerPurchase = 0;
            cfg.PremiumCurrencyCostPerPurchase = 0;
            cfg.ValidateOrThrow();
            return cfg;
        }
    }
}
