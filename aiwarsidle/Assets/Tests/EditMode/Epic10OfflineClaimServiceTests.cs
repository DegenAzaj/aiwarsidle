using AIWarsIdle.GameCore.Config;
using AIWarsIdle.GameCore.Domain;
using AIWarsIdle.GameCore.Services;
using NUnit.Framework;
using UnityEngine;

namespace AIWarsIdle.Tests.EditMode
{
    public sealed class Epic10OfflineClaimServiceTests
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
            cfg.OfflineEfficiency = 0.6;
            cfg.ValidateOrThrow();
            return cfg;
        }

        [Test]
        public void BankOfflineGain_WhenOfflineBelowCap_AddsPending()
        {
            var state = new GameState { LastLoginUnixSeconds = 0 };
            state.GeneratorLevels[0] = 1;

            var cfg = CreateValidBalanceConfig(generatorOutput: 10);
            var economy = new EconomyService(state);
            var production = new ProductionService(state, cfg, economy);
            var offline = new OfflineClaimService(state, cfg, production, economy);

            offline.BankOfflineGain(nowUnixSeconds: 100);

            Assert.AreEqual(600.0, offline.PendingOfflineGain);
        }

        [Test]
        public void BankOfflineGain_WhenOfflineAboveCap_UsesCap()
        {
            var state = new GameState { LastLoginUnixSeconds = 0 };
            state.GeneratorLevels[0] = 1;

            var cfg = CreateValidBalanceConfig(generatorOutput: 10);
            var economy = new EconomyService(state);
            var production = new ProductionService(state, cfg, economy);
            var offline = new OfflineClaimService(state, cfg, production, economy);

            offline.BankOfflineGain(nowUnixSeconds: 20 * 60 * 60);

            Assert.AreEqual(10.0 * (12 * 60 * 60) * 0.6, offline.PendingOfflineGain);
        }

        [Test]
        public void BankOfflineGain_StoresRawAndEffectiveOfflineSeconds_ForUi()
        {
            var state = new GameState { LastLoginUnixSeconds = 0 };
            state.GeneratorLevels[0] = 1;

            var cfg = CreateValidBalanceConfig(generatorOutput: 10);
            var economy = new EconomyService(state);
            var production = new ProductionService(state, cfg, economy);
            var offline = new OfflineClaimService(state, cfg, production, economy);

            offline.BankOfflineGain(nowUnixSeconds: 20 * 60 * 60);

            Assert.AreEqual(20 * 60 * 60, offline.LastBankedOfflineRawSeconds);
            Assert.AreEqual(12 * 60 * 60, offline.LastBankedOfflineEffectiveSeconds);
        }

        [Test]
        public void Claim_WithMultiplier1_And_2_AddsCorrectAndClearsPending()
        {
            var state = new GameState { LastLoginUnixSeconds = 0 };
            state.GeneratorLevels[0] = 1;

            var cfg = CreateValidBalanceConfig(generatorOutput: 10);
            var economy = new EconomyService(state);
            var production = new ProductionService(state, cfg, economy);
            var offline = new OfflineClaimService(state, cfg, production, economy);

            offline.BankOfflineGain(nowUnixSeconds: 100);
            Assert.AreEqual(600.0, offline.PendingOfflineGain);

            offline.Claim(multiplier: 1);
            Assert.AreEqual(600.0, state.SoftCurrency);
            Assert.AreEqual(0.0, offline.PendingOfflineGain);

            offline.BankOfflineGain(nowUnixSeconds: 200);
            Assert.AreEqual(600.0, offline.PendingOfflineGain);

            offline.Claim(multiplier: 2);
            Assert.AreEqual(1800.0, state.SoftCurrency);
            Assert.AreEqual(0.0, offline.PendingOfflineGain);
        }

        [Test]
        public void MarkBackgrounded_ResetsOfflineWindowStart_ForResumeFlow()
        {
            var state = new GameState { LastLoginUnixSeconds = 0 };
            state.GeneratorLevels[0] = 1;

            var cfg = CreateValidBalanceConfig(generatorOutput: 10);
            var economy = new EconomyService(state);
            var production = new ProductionService(state, cfg, economy);
            var offline = new OfflineClaimService(state, cfg, production, economy);

            offline.MarkBackgrounded(nowUnixSeconds: 500);
            offline.BankOfflineGain(nowUnixSeconds: 560);

            Assert.AreEqual(560, state.LastLoginUnixSeconds);
            Assert.AreEqual(10.0 * 60 * 0.6, offline.PendingOfflineGain);
            Assert.AreEqual(60, offline.LastBankedOfflineRawSeconds);
        }
    }
}
