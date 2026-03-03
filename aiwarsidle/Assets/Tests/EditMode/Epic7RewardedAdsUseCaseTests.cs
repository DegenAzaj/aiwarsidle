using AIWarsIdle.GameCore.Config;
using AIWarsIdle.GameCore.Domain;
using AIWarsIdle.GameCore.Services;
using AIWarsIdle.PvP.Config;
using AIWarsIdle.PvP.Services;
using NUnit.Framework;
using UnityEngine;

namespace AIWarsIdle.Tests.EditMode
{
    public sealed class Epic7RewardedAdsUseCaseTests
    {
        private sealed class FakeAdsService : IAdsService
        {
            public int ShowCalls { get; private set; }

            public void ShowRewardedAd(System.Action onSuccess)
            {
                ShowCalls++;
                onSuccess?.Invoke();
            }
        }

        private sealed class FakeAnalyticsService : IAnalyticsService
        {
            public int TrackCalls { get; private set; }
            public string LastEventName { get; private set; }

            public void Track(string name, params AnalyticsParam[] parameters)
            {
                TrackCalls++;
                LastEventName = name;
            }
        }

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

        [Test]
        public void OfflineX2Rewarded_ClaimsDoubleAndTracks()
        {
            var state = new GameState();
            state.GeneratorLevels[0] = 1;
            state.LastLoginUnixSeconds = 0;

            var economy = new EconomyService(state);
            var balance = CreateValidBalanceConfig(generatorOutput: 10);
            var production = new ProductionService(state, balance, economy);
            var offline = new OfflineClaimService(state, balance, production, economy);

            offline.RecalculatePending(nowUnixSeconds: 100); // pending = 10 * 100 = 1000
            Assert.AreEqual(1000.0, offline.PendingOfflineGain);

            var attacks = new PvpAttackChargesService(state, CreatePvpAttacksConfig());
            var ads = new FakeAdsService();
            var analytics = new FakeAnalyticsService();

            var uc = new RewardedAdsUseCaseService(ads, offline, attacks, analytics);

            Assert.IsTrue(uc.TryShowOfflineClaimX2(nowUnixSeconds: 100));
            Assert.AreEqual(1, ads.ShowCalls);
            Assert.AreEqual(2000.0, economy.Balance);
            Assert.GreaterOrEqual(analytics.TrackCalls, 1);
        }

        [Test]
        public void DailyPvpAttackRewarded_AddsOne_AttemptsOnlyOncePerUtcDay()
        {
            var state = new GameState { PvpAttacksRemaining = 0, LastPvpAdAttackClaimUnixSeconds = 0 };
            var attacks = new PvpAttackChargesService(state, CreatePvpAttacksConfig());

            var balance = CreateValidBalanceConfig(generatorOutput: 1);
            var economy = new EconomyService(state);
            var production = new ProductionService(state, balance, economy);
            var offline = new OfflineClaimService(state, balance, production, economy);

            var ads = new FakeAdsService();
            var analytics = new FakeAnalyticsService();
            var uc = new RewardedAdsUseCaseService(ads, offline, attacks, analytics);

            Assert.IsTrue(uc.TryShowDailyPvpAttack(nowUnixSeconds: 1));
            Assert.AreEqual(1, state.PvpAttacksRemaining);
            Assert.AreEqual(1, ads.ShowCalls);

            Assert.IsFalse(uc.TryShowDailyPvpAttack(nowUnixSeconds: 10));
            Assert.AreEqual(1, state.PvpAttacksRemaining);
            Assert.AreEqual(1, ads.ShowCalls);

            // new UTC day
            state.PvpAttacksRemaining = 0;
            state.LastPvpAdAttackClaimUnixSeconds = 86_399;
            state.NextPvpAttackRegenAtUnixSeconds = 86_400 + 1_000_000_000L; // keep regen from capping attacks during Tick()
            Assert.IsTrue(uc.TryShowDailyPvpAttack(nowUnixSeconds: 86_400));
            Assert.AreEqual(1, state.PvpAttacksRemaining);
        }

        [Test]
        public void DailyPvpAttackRewarded_WhenCapped_DoesNotShowAd()
        {
            var state = new GameState { PvpAttacksRemaining = 5, LastPvpAdAttackClaimUnixSeconds = 0 };
            var attacks = new PvpAttackChargesService(state, CreatePvpAttacksConfig());

            var balance = CreateValidBalanceConfig(generatorOutput: 1);
            var economy = new EconomyService(state);
            var production = new ProductionService(state, balance, economy);
            var offline = new OfflineClaimService(state, balance, production, economy);

            var ads = new FakeAdsService();
            var uc = new RewardedAdsUseCaseService(ads, offline, attacks);

            Assert.IsFalse(uc.TryShowDailyPvpAttack(nowUnixSeconds: 1));
            Assert.AreEqual(0, ads.ShowCalls);
        }
    }
}
