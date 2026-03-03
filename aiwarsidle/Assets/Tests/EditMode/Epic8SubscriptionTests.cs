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
    public sealed class Epic8SubscriptionTests
    {
        private static BalanceConfig CreateValidBalanceConfig(double generatorOutput)
        {
            var cfg = ScriptableObject.CreateInstance<BalanceConfig>();
            for (var i = 0; i < GameState.GeneratorCount; i++)
            {
                cfg.GeneratorBaseCosts[i] = 1;
                cfg.GeneratorCostGrowthFactors[i] = 1.2;
                cfg.GeneratorBaseOutputs[i] = generatorOutput;
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

        private static PvpConfig CreateValidPvpConfig()
        {
            var pvp = ScriptableObject.CreateInstance<PvpConfig>();
            pvp.PowerVarianceMin = 1.0f;
            pvp.PowerVarianceMax = 1.0f;
            pvp.StrategyMultiplierAggressive = 1.0f;
            pvp.StrategyMultiplierStable = 1.0f;
            pvp.StrategyMultiplierRisky = 1.0f;
            pvp.StabilityMultiplierMin = 1.0f;
            pvp.StabilityMultiplierMax = 1.0f;

            pvp.PrestigePvpPowerPerPrestige = 0.0;
            pvp.PermanentPvpPowerPerLevel = 0.0;
            pvp.SectorPvpPowerPerSector = 0.0;

            pvp.ProdToPvpMaxBonus = 0.5;
            pvp.ProdToPvpHalfCapPps = 100.0;
            pvp.ValidateOrThrow();
            return pvp;
        }

        [Test]
        public void ProductionService_WhenSubscriptionActive_DoublesPps()
        {
            var state = new GameState();
            state.GeneratorLevels[0] = 1;

            var economy = new EconomyService(state);
            var balance = CreateValidBalanceConfig(generatorOutput: 10);

            var sub = new SubscriptionService(isActive: false);
            var production = new ProductionService(state, balance, economy, subscription: sub);

            var without = production.CalculateProductionPerSecond();

            sub.SetActive(true);
            var with = production.CalculateProductionPerSecond();

            Assert.AreEqual(without * 2.0, with, 1e-9);
        }

        [Test]
        public void Snapshot_PvpPower_DoesNotChange_When_Subscription_Toggles()
        {
            var state = new GameState();
            state.GeneratorLevels[0] = 10;

            var economy = new EconomyService(state);
            var balance = CreateValidBalanceConfig(generatorOutput: 10);

            var sub = new SubscriptionService(isActive: false);
            var production = new ProductionService(state, balance, economy, subscription: sub);
            var snapshotService = new SnapshotService(state, CreateValidPvpConfig(), production);

            var before = snapshotService.BuildSnapshot().PvpPower;

            sub.SetActive(true);
            var after = snapshotService.BuildSnapshot().PvpPower;

            Assert.AreEqual(before, after, 1e-9);
        }

        [Test]
        public void InterstitialAdsService_WhenSubscriptionActive_DoesNotShow()
        {
            var sub = new SubscriptionService(isActive: true);
            var ads = new InterstitialAdsService(sub);

            var closedCalls = 0;
            Assert.IsFalse(ads.TryShowInterstitial("level_end", () => closedCalls++));
            Assert.AreEqual(0, closedCalls);

            sub.SetActive(false);
            Assert.IsTrue(ads.TryShowInterstitial("level_end", () => closedCalls++));
            Assert.AreEqual(1, closedCalls);
        }
    }
}

