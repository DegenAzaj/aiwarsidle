using System;
using AIWarsIdle.GameCore.Config;
using AIWarsIdle.GameCore.Domain;
using AIWarsIdle.GameCore.Services;
using AIWarsIdle.PvP.Config;
using AIWarsIdle.PvP.Services;
using NUnit.Framework;
using UnityEngine;

namespace AIWarsIdle.Tests
{
    public sealed class Epic5SnapshotLeagueTests
    {
        private static BalanceConfig CreateValidBalanceConfig()
        {
            var cfg = ScriptableObject.CreateInstance<BalanceConfig>();
            cfg.GeneratorBaseCosts = new[] { 10.0, 100.0, 1_000.0, 10_000.0, 100_000.0 };
            cfg.GeneratorCostGrowthFactors = new[] { 1.15, 1.15, 1.15, 1.15, 1.15 };
            cfg.GeneratorBaseOutputs = new[] { 1.0, 5.0, 25.0, 125.0, 625.0 };
            cfg.MilestoneEveryLevels = 0;
            cfg.MilestoneMultiplier = 2.0;
            cfg.PrestigeThresholdBase = 1_000;
            cfg.PrestigeThresholdGrowthFactor = 1.6;
            cfg.PermanentUpgradeCap = 10;
            cfg.PrestigeMultiplierIncrease = 0.05;
            cfg.OfflineCapSeconds = 12 * 60 * 60;
            cfg.ValidateOrThrow();
            return cfg;
        }

        private static PvpConfig CreateValidPvpConfig()
        {
            var pvp = ScriptableObject.CreateInstance<PvpConfig>();
            pvp.PowerVarianceMin = 0.95f;
            pvp.PowerVarianceMax = 1.05f;
            pvp.StrategyMultiplierAggressive = 1.05f;
            pvp.StrategyMultiplierStable = 1.0f;
            pvp.StrategyMultiplierRisky = 1.1f;
            pvp.StabilityMultiplierMin = 0.9f;
            pvp.StabilityMultiplierMax = 1.2f;

            pvp.PrestigePvpPowerPerPrestige = 2.0;
            pvp.PermanentPvpPowerPerLevel = 3.0;
            pvp.SectorPvpPowerPerSector = 4.0;

            pvp.ProdToPvpMaxBonus = 0.5;
            pvp.ProdToPvpHalfCapPps = 100.0;

            pvp.ValidateOrThrow();
            return pvp;
        }

        [Test]
        public void Snapshot_PvpPower_DoesNotChange_When_Overclock_Multiplies_Production()
        {
            var state = new GameState();
            state.GeneratorLevels[0] = 10;

            var balance = CreateValidBalanceConfig();
            var economy = new EconomyService(state);

            var overclockCfg = ScriptableObject.CreateInstance<OverclockConfig>();
            overclockCfg.DurationSeconds = 10;
            overclockCfg.RegenSeconds = 90;
            overclockCfg.MaxCharges = 2;
            overclockCfg.ProductionMultiplier = 3.0;
            overclockCfg.PvpAttackMultiplier = 1.2;
            overclockCfg.FreshCaptureStabilityGrowthMultiplier = 1.1;

            var overclock = new OverclockService(state, overclockCfg);
            state.Overclock.Charges = 1;

            var production = new ProductionService(state, balance, economy, overclock);
            var pvp = CreateValidPvpConfig();

            var snapshotService = new SnapshotService(state, pvp, production);
            var before = snapshotService.BuildSnapshot().PvpPower;

            Assert.IsTrue(overclock.Activate(nowUnixSeconds: 1000));
            var after = snapshotService.BuildSnapshot().PvpPower;

            Assert.AreEqual(before, after, 1e-9);
        }

        [Test]
        public void Snapshot_PvpPower_Grows_With_Prestige_Permanent_And_Sectors()
        {
            var state = new GameState();
            var balance = CreateValidBalanceConfig();
            var economy = new EconomyService(state);
            var production = new ProductionService(state, balance, economy);
            var pvp = CreateValidPvpConfig();

            var snapshotService = new SnapshotService(state, pvp, production);

            var basePower = snapshotService.BuildSnapshot().PvpPower;

            state.PrestigeCount += 1;
            var afterPrestige = snapshotService.BuildSnapshot().PvpPower;
            Assert.Greater(afterPrestige, basePower);

            state.PermanentUpgradeLevel += 1;
            var afterPermanent = snapshotService.BuildSnapshot().PvpPower;
            Assert.Greater(afterPermanent, afterPrestige);

            state.MapState.Sectors = new[]
            {
                new SectorState { SectorId = 1, OwnerPlayerId = 1, Stability = 0f },
                new SectorState { SectorId = 2, OwnerPlayerId = 1, Stability = 0f }
            };
            var afterSectors = snapshotService.BuildSnapshot().PvpPower;
            Assert.Greater(afterSectors, afterPermanent);
        }

        [Test]
        public void LeagueService_Points_Map_To_League_By_Thresholds()
        {
            var cfg = ScriptableObject.CreateInstance<LeagueConfig>();
            cfg.LeaguePointThresholds = new[] { 0, 100, 200, 500, 1000 };
            cfg.SeasonMode = LeagueSeasonMode.CalendarMonthUtc;
            cfg.ValidateOrThrow();

            var state = new GameState();
            var service = new LeagueService(state, cfg);

            var now = new DateTimeOffset(2026, 3, 3, 0, 0, 0, TimeSpan.Zero).ToUnixTimeSeconds();

            service.AddSeasonPoints(now, 50);
            Assert.AreEqual(0, state.League);

            service.AddSeasonPoints(now, 60);
            Assert.AreEqual(1, state.League);

            service.AddSeasonPoints(now, 200);
            Assert.AreEqual(2, state.League);

            service.AddSeasonPoints(now, -10_000);
            Assert.AreEqual(0, state.SeasonPoints);
            Assert.AreEqual(0, state.League);
        }

        [Test]
        public void LeagueService_ResetSeason_Resets_Points_And_Updates_LeagueSeasonId()
        {
            var cfg = ScriptableObject.CreateInstance<LeagueConfig>();
            cfg.LeaguePointThresholds = new[] { 0, 100, 200, 500, 1000 };
            cfg.SeasonMode = LeagueSeasonMode.CalendarMonthUtc;
            cfg.ValidateOrThrow();

            var state = new GameState();
            var service = new LeagueService(state, cfg);

            var march = new DateTimeOffset(2026, 3, 3, 0, 0, 0, TimeSpan.Zero).ToUnixTimeSeconds();
            service.ResetSeasonIfNeeded(march);
            service.AddSeasonPoints(march, 250);
            var marchSeasonId = state.LeagueSeasonId;
            Assert.Greater(state.SeasonPoints, 0);
            Assert.Greater(state.League, 0);

            var april = new DateTimeOffset(2026, 4, 1, 0, 0, 0, TimeSpan.Zero).ToUnixTimeSeconds();
            service.ResetSeasonIfNeeded(april);

            Assert.AreNotEqual(marchSeasonId, state.LeagueSeasonId);
            Assert.AreEqual(0, state.SeasonPoints);
            Assert.AreEqual(0, state.League);
        }

        [Test]
        public void PvpConfig_Validation_Rejects_Invalid_ProdToPvp_Settings()
        {
            var cfg = CreateValidPvpConfig();

            cfg.ProdToPvpMaxBonus = 1.1;
            Assert.Throws<InvalidOperationException>(() => cfg.ValidateOrThrow());

            cfg.ProdToPvpMaxBonus = 0.5;
            cfg.ProdToPvpHalfCapPps = 0;
            Assert.Throws<InvalidOperationException>(() => cfg.ValidateOrThrow());
        }
    }
}

