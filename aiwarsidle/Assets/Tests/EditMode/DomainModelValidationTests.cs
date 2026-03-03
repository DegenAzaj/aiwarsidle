using System;
using AIWarsIdle.GameCore.Config;
using AIWarsIdle.GameCore.Domain;
using AIWarsIdle.GameCore.Validation;
using AIWarsIdle.GameCore.Services;
using NUnit.Framework;
using UnityEngine;

namespace AIWarsIdle.Tests
{
    public sealed class DomainModelValidationTests
    {
        [Test]
        public void SectorState_Validation_Allows_Valid_Values()
        {
            var sector = new SectorState
            {
                SectorId = 1,
                Stability = 0f,
                LastCombatUnixSeconds = 0,
                CapturedUnixSeconds = 0
            };

            Assert.DoesNotThrow(() => DomainValidation.ValidateSectorState(sector));
        }

        [Test]
        public void SectorState_Validation_Rejects_Invalid_Stability()
        {
            var sector = new SectorState
            {
                SectorId = 1,
                Stability = 101f
            };

            Assert.Throws<ArgumentOutOfRangeException>(() => DomainValidation.ValidateSectorState(sector));
        }

        [Test]
        public void SectorState_Validation_Rejects_Negative_Timestamps()
        {
            var sector = new SectorState
            {
                SectorId = 1,
                Stability = 50f,
                LastCombatUnixSeconds = -1
            };

            Assert.Throws<ArgumentOutOfRangeException>(() => DomainValidation.ValidateSectorState(sector));
        }

        [Test]
        public void OverclockState_Validation_Rejects_OutOfRange_Charges()
        {
            var state = new OverclockState
            {
                Charges = 3,
                ActiveUntilUnixSeconds = 0,
                NextChargeAtUnixSeconds = 0
            };

            Assert.Throws<ArgumentOutOfRangeException>(() => DomainValidation.ValidateOverclockState(state, maxCharges: 2));
        }

        [Test]
        public void OverclockState_Validation_Rejects_Negative_Timestamps()
        {
            var state = new OverclockState
            {
                Charges = 1,
                ActiveUntilUnixSeconds = -1
            };

            Assert.Throws<ArgumentOutOfRangeException>(() => DomainValidation.ValidateOverclockState(state, maxCharges: 2));
        }
    }

    public sealed class Epic3EconomyTests
    {
        private static BalanceConfig CreateValidBalanceConfig()
        {
            var cfg = ScriptableObject.CreateInstance<BalanceConfig>();
            cfg.GeneratorBaseCosts = new[] { 10.0, 100.0, 1_000.0, 10_000.0, 100_000.0 };
            cfg.GeneratorCostGrowthFactors = new[] { 1.15, 1.15, 1.15, 1.15, 1.15 };
            cfg.GeneratorBaseOutputs = new[] { 1.0, 5.0, 25.0, 125.0, 625.0 };
            cfg.MilestoneEveryLevels = 25;
            cfg.MilestoneMultiplier = 2.0;

            cfg.PrestigeThresholdBase = 1_000;
            cfg.PrestigeThresholdGrowthFactor = 1.6;
            cfg.PermanentUpgradeCap = 10;
            cfg.PrestigeMultiplierIncrease = 0.05;

            cfg.OfflineCapSeconds = 12 * 60 * 60;
            cfg.ValidateOrThrow();
            return cfg;
        }

        [Test]
        public void BalanceConfig_Validation_Rejects_Invalid_Generator_Counts()
        {
            var cfg = ScriptableObject.CreateInstance<BalanceConfig>();
            cfg.GeneratorBaseCosts = new double[0];
            cfg.GeneratorCostGrowthFactors = new double[0];
            cfg.GeneratorBaseOutputs = new double[0];

            Assert.Throws<InvalidOperationException>(() => cfg.ValidateOrThrow());
        }

        [Test]
        public void EconomyService_SpendMoreThanBalance_Fails_And_DoesNotChange_State()
        {
            var state = new GameState { SoftCurrency = 10.0, LifetimeEarnedSoftCurrency = 10.0 };
            var economy = new EconomyService(state);

            var ok = economy.SpendCurrency(11.0);

            Assert.IsFalse(ok);
            Assert.AreEqual(10.0, state.SoftCurrency);
            Assert.AreEqual(10.0, state.LifetimeEarnedSoftCurrency);
        }

        [Test]
        public void EconomyService_AddSpend_Handles_Extremes_And_Validates_Input()
        {
            var state = new GameState();
            var economy = new EconomyService(state);

            economy.AddCurrency(0);
            Assert.AreEqual(0, state.SoftCurrency);
            Assert.AreEqual(0, state.LifetimeEarnedSoftCurrency);

            economy.AddCurrency(0.0000001, CurrencySource.Debug);
            Assert.Greater(state.SoftCurrency, 0);
            Assert.Greater(state.LifetimeEarnedSoftCurrency, 0);

            Assert.IsTrue(economy.SpendCurrency(0));

            Assert.Throws<ArgumentOutOfRangeException>(() => economy.AddCurrency(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => economy.AddCurrency(double.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => economy.AddCurrency(double.PositiveInfinity));

            Assert.Throws<ArgumentOutOfRangeException>(() => economy.SpendCurrency(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => economy.SpendCurrency(double.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => economy.SpendCurrency(double.PositiveInfinity));

            economy.AddCurrency(1e18, CurrencySource.Debug);
            Assert.GreaterOrEqual(state.SoftCurrency, 1e18);
            Assert.GreaterOrEqual(state.LifetimeEarnedSoftCurrency, 1e18);
        }

        [Test]
        public void ProductionService_Pps_Increases_After_Upgrade()
        {
            var state = new GameState();
            var cfg = CreateValidBalanceConfig();
            var economy = new EconomyService(state);
            var production = new ProductionService(state, cfg, economy);
            var upgrades = new UpgradeService(state, cfg, economy);

            economy.AddCurrency(1_000_000, CurrencySource.Debug);
            var before = production.CalculateProductionPerSecond();

            upgrades.UpgradeGeneratorX1(0);
            var after = production.CalculateProductionPerSecond();

            Assert.Greater(after, before);
        }

        [Test]
        public void ProductionService_OfflineGain_Is_Capped_At_12h()
        {
            var state = new GameState();
            var cfg = CreateValidBalanceConfig();
            var economy = new EconomyService(state);
            var production = new ProductionService(state, cfg, economy);

            state.GeneratorLevels[0] = 10;

            var gain12h = production.CalculateOfflineGain(12 * 60 * 60);
            var gain20h = production.CalculateOfflineGain(20 * 60 * 60);

            Assert.AreEqual(gain12h, gain20h);
        }

        [Test]
        public void ProductionService_Is_Deterministic_For_Same_Inputs()
        {
            var state = new GameState();
            var cfg = CreateValidBalanceConfig();
            var economy = new EconomyService(state);
            var production = new ProductionService(state, cfg, economy);

            state.GeneratorLevels[0] = 10;
            state.GeneratorLevels[3] = 2;
            state.PermanentUpgradeLevel = 4;

            var pps1 = production.CalculateProductionPerSecond();
            var pps2 = production.CalculateProductionPerSecond();
            Assert.AreEqual(pps1, pps2);

            var gain1 = production.CalculateOfflineGain(1234);
            var gain2 = production.CalculateOfflineGain(1234);
            Assert.AreEqual(gain1, gain2);
        }

        [Test]
        public void UpgradeService_Cost_Grows_Exponentially()
        {
            var state = new GameState();
            var cfg = CreateValidBalanceConfig();
            var economy = new EconomyService(state);
            var upgrades = new UpgradeService(state, cfg, economy);

            state.GeneratorLevels[0] = 0;
            var c0 = upgrades.GetUpgradeCost(0);

            state.GeneratorLevels[0] = 1;
            var c1 = upgrades.GetUpgradeCost(0);

            state.GeneratorLevels[0] = 2;
            var c2 = upgrades.GetUpgradeCost(0);

            Assert.Greater(c1, c0);
            Assert.Greater(c2, c1);
        }

        [Test]
        public void UpgradeService_Without_Funds_Does_Not_Change_Level()
        {
            var state = new GameState();
            var cfg = CreateValidBalanceConfig();
            var economy = new EconomyService(state);
            var upgrades = new UpgradeService(state, cfg, economy);

            var upgraded = upgrades.UpgradeGenerator(0);

            Assert.AreEqual(0, upgraded);
            Assert.AreEqual(0, state.GeneratorLevels[0]);
            Assert.AreEqual(0, state.SoftCurrency);
        }

        [Test]
        public void UpgradeService_X10_Spends_Sum_Of_Next_10_Costs()
        {
            var state = new GameState();
            var cfg = CreateValidBalanceConfig();
            var economy = new EconomyService(state);
            var upgrades = new UpgradeService(state, cfg, economy);

            const int generatorId = 0;
            var baseCost = cfg.GeneratorBaseCosts[generatorId];
            var growth = cfg.GeneratorCostGrowthFactors[generatorId];

            double required = 0;
            for (var level = 0; level < 10; level++)
            {
                required += baseCost * Math.Pow(growth, level);
            }

            economy.AddCurrency(required, CurrencySource.Debug);

            var upgraded = upgrades.UpgradeGeneratorX10(generatorId);

            Assert.AreEqual(10, upgraded);
            Assert.AreEqual(10, state.GeneratorLevels[generatorId]);
            Assert.LessOrEqual(state.SoftCurrency, 1e-9);
        }

        [Test]
        public void PrestigeService_Uses_Lifetime_Earned_And_Clamps_Permanent_Upgrade()
        {
            var state = new GameState
            {
                SoftCurrency = 123,
                LifetimeEarnedSoftCurrency = 1_000_000,
                PermanentUpgradeLevel = 10,
                PrestigeCount = 0
            };

            var cfg = CreateValidBalanceConfig();
            cfg.PermanentUpgradeCap = 10;

            var prestige = new PrestigeService(state, cfg);
            Assert.IsTrue(prestige.CanPrestige());

            prestige.ExecutePrestige();

            Assert.AreEqual(0, state.SoftCurrency);
            Assert.AreEqual(1, state.PrestigeCount);
            Assert.AreEqual(10, state.PermanentUpgradeLevel);
            Assert.AreEqual(1_000_000, state.LifetimeEarnedSoftCurrency);
        }

        [Test]
        public void OfflineClaimService_Claim_Adds_Currency_And_LifetimeEarned()
        {
            var state = new GameState
            {
                LastLoginUnixSeconds = 0
            };

            var cfg = CreateValidBalanceConfig();
            var economy = new EconomyService(state);
            var production = new ProductionService(state, cfg, economy);
            var offline = new OfflineClaimService(state, cfg, production, economy);

            state.GeneratorLevels[0] = 10;

            offline.BankOfflineGain(nowUnixSeconds: 3600);
            Assert.Greater(offline.PendingOfflineGain, 0);

            offline.Claim(multiplier: 2);
            Assert.Greater(state.SoftCurrency, 0);
            Assert.AreEqual(state.SoftCurrency, state.LifetimeEarnedSoftCurrency);
            Assert.AreEqual(0, offline.PendingOfflineGain);
        }
    }
}
