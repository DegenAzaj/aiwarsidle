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
    public sealed class OverclockEpic4Tests
    {
        [Test]
        public void OverclockConfig_Validation_Rejects_Invalid_Values()
        {
            var cfg = ScriptableObject.CreateInstance<OverclockConfig>();
            cfg.DurationSeconds = 0;
            Assert.Throws<InvalidOperationException>(() => cfg.ValidateOrThrow());

            cfg.DurationSeconds = 10;
            cfg.RegenSeconds = 0;
            Assert.Throws<InvalidOperationException>(() => cfg.ValidateOrThrow());

            cfg.RegenSeconds = 90;
            cfg.MaxCharges = -1;
            Assert.Throws<InvalidOperationException>(() => cfg.ValidateOrThrow());

            cfg.MaxCharges = 2;
            cfg.ProductionMultiplier = 0.5;
            Assert.Throws<InvalidOperationException>(() => cfg.ValidateOrThrow());
        }

        [Test]
        public void OverclockService_Activate_Spends_Charge_And_Sets_ActiveUntil()
        {
            var state = new GameState();
            var cfg = ScriptableObject.CreateInstance<OverclockConfig>();
            cfg.DurationSeconds = 10;
            cfg.RegenSeconds = 90;
            cfg.MaxCharges = 2;
            cfg.ProductionMultiplier = 3.0;
            cfg.PvpAttackMultiplier = 1.2;
            cfg.FreshCaptureStabilityGrowthMultiplier = 1.1;

            var service = new OverclockService(state, cfg);
            state.Overclock.Charges = 2;

            var ok = service.Activate(nowUnixSeconds: 1000);

            Assert.IsTrue(ok);
            Assert.AreEqual(1, state.Overclock.Charges);
            Assert.AreEqual(1010, state.Overclock.ActiveUntilUnixSeconds);
        }

        [Test]
        public void OverclockService_Cannot_Activate_While_Active()
        {
            var state = new GameState();
            var cfg = ScriptableObject.CreateInstance<OverclockConfig>();
            cfg.DurationSeconds = 10;
            cfg.RegenSeconds = 90;
            cfg.MaxCharges = 2;
            cfg.ProductionMultiplier = 3.0;
            cfg.PvpAttackMultiplier = 1.2;
            cfg.FreshCaptureStabilityGrowthMultiplier = 1.1;

            var service = new OverclockService(state, cfg);
            state.Overclock.Charges = 2;

            Assert.IsTrue(service.Activate(nowUnixSeconds: 1000));
            Assert.IsFalse(service.CanActivate(nowUnixSeconds: 1001));
            Assert.IsFalse(service.Activate(nowUnixSeconds: 1001));
        }

        [Test]
        public void OverclockService_Regen_Adds_One_Charge_Per_90s_Clamped_To_Max()
        {
            var state = new GameState();
            var cfg = ScriptableObject.CreateInstance<OverclockConfig>();
            cfg.DurationSeconds = 10;
            cfg.RegenSeconds = 90;
            cfg.MaxCharges = 2;
            cfg.ProductionMultiplier = 3.0;
            cfg.PvpAttackMultiplier = 1.2;
            cfg.FreshCaptureStabilityGrowthMultiplier = 1.1;

            var service = new OverclockService(state, cfg);

            state.Overclock.Charges = 0;
            state.Overclock.NextChargeAtUnixSeconds = 1090;

            service.Tick(nowUnixSeconds: 1089);
            Assert.AreEqual(0, state.Overclock.Charges);

            service.Tick(nowUnixSeconds: 1090);
            Assert.AreEqual(1, state.Overclock.Charges);

            state.Overclock.NextChargeAtUnixSeconds = 1180;
            service.Tick(nowUnixSeconds: 1180);
            Assert.AreEqual(2, state.Overclock.Charges);
            Assert.AreEqual(0, state.Overclock.NextChargeAtUnixSeconds);
        }

        [Test]
        public void Overclock_Multipliers_Apply_Only_While_Active()
        {
            var state = new GameState();
            var cfg = ScriptableObject.CreateInstance<OverclockConfig>();
            cfg.DurationSeconds = 10;
            cfg.RegenSeconds = 90;
            cfg.MaxCharges = 2;
            cfg.ProductionMultiplier = 3.0;
            cfg.PvpAttackMultiplier = 1.2;
            cfg.FreshCaptureStabilityGrowthMultiplier = 1.1;

            var service = new OverclockService(state, cfg);
            state.Overclock.Charges = 1;

            Assert.AreEqual(1.0, service.GetProductionMultiplier(nowUnixSeconds: 1000));

            Assert.IsTrue(service.Activate(nowUnixSeconds: 1000));
            Assert.AreEqual(3.0, service.GetProductionMultiplier(nowUnixSeconds: 1005));
            Assert.AreEqual(1.2, service.GetPvpAttackMultiplier(nowUnixSeconds: 1005));
            Assert.AreEqual(1.1, service.GetFreshCaptureStabilityGrowthMultiplier(nowUnixSeconds: 1005));

            service.Tick(nowUnixSeconds: 1010);
            Assert.AreEqual(1.0, service.GetProductionMultiplier(nowUnixSeconds: 1010));
        }

        [Test]
        public void ProductionService_Overclock_Multiplies_Pps_When_Active()
        {
            var state = new GameState();
            var balance = ScriptableObject.CreateInstance<BalanceConfig>();
            balance.GeneratorBaseCosts = new[] { 10.0, 100.0, 1_000.0, 10_000.0, 100_000.0 };
            balance.GeneratorCostGrowthFactors = new[] { 1.15, 1.15, 1.15, 1.15, 1.15 };
            balance.GeneratorBaseOutputs = new[] { 1.0, 5.0, 25.0, 125.0, 625.0 };
            balance.MilestoneEveryLevels = 0;
            balance.MilestoneMultiplier = 2.0;
            balance.PrestigeThresholdBase = 1_000;
            balance.PrestigeThresholdGrowthFactor = 1.6;
            balance.PermanentUpgradeCap = 10;
            balance.PrestigeMultiplierIncrease = 0.05;
            balance.OfflineCapSeconds = 12 * 60 * 60;
            balance.ValidateOrThrow();

            state.GeneratorLevels[0] = 10;

            var economy = new EconomyService(state);
            var overclockCfg = ScriptableObject.CreateInstance<OverclockConfig>();
            overclockCfg.DurationSeconds = 10;
            overclockCfg.RegenSeconds = 90;
            overclockCfg.MaxCharges = 2;
            overclockCfg.ProductionMultiplier = 3.0;
            overclockCfg.PvpAttackMultiplier = 1.2;
            overclockCfg.FreshCaptureStabilityGrowthMultiplier = 1.1;

            var overclock = new OverclockService(state, overclockCfg);
            state.Overclock.Charges = overclockCfg.MaxCharges;
            var production = new ProductionService(state, balance, economy, overclock);

            var basePps = production.CalculateProductionPerSecond(nowUnixSeconds: 1000);
            Assert.IsTrue(overclock.Activate(nowUnixSeconds: 1000));
            var boosted = production.CalculateProductionPerSecond(nowUnixSeconds: 1001);

            Assert.AreEqual(basePps * 3.0, boosted, 1e-9);
        }

        [Test]
        public void BattleSimService_Overclock_AttackMultiplier_Changes_Result_Deterministically()
        {
            var pvp = ScriptableObject.CreateInstance<PvpConfig>();
            pvp.PowerVarianceMin = 1.0f;
            pvp.PowerVarianceMax = 1.0f;
            pvp.StrategyMultiplierAggressive = 1.0f;
            pvp.StrategyMultiplierStable = 1.0f;
            pvp.StrategyMultiplierRisky = 1.0f;
            pvp.StabilityMultiplierMin = 1.0f;
            pvp.StabilityMultiplierMax = 1.0f;
            pvp.ValidateOrThrow();

            var game = new GameState();
            var overclockCfg = ScriptableObject.CreateInstance<OverclockConfig>();
            overclockCfg.DurationSeconds = 10;
            overclockCfg.RegenSeconds = 90;
            overclockCfg.MaxCharges = 2;
            overclockCfg.ProductionMultiplier = 3.0;
            overclockCfg.PvpAttackMultiplier = 1.2;
            overclockCfg.FreshCaptureStabilityGrowthMultiplier = 1.1;

            var overclock = new OverclockService(game, overclockCfg);
            game.Overclock.Charges = 1;

            var sim = new BattleSimService(pvp, overclock);

            var without = sim.Simulate(attackerPvpPower: 100, defenderPvpPower: 110, strategy: AttackStrategy.Stable, stability: 0f, seed: 123);

            Assert.IsTrue(overclock.Activate(nowUnixSeconds: 1000));
            var withOverclock = sim.Simulate(attackerPvpPower: 100, defenderPvpPower: 110, strategy: AttackStrategy.Stable, stability: 0f, nowUnixSeconds: 1001, seed: 123);

            Assert.IsFalse(without.Win);
            Assert.IsTrue(withOverclock.Win);
        }

        [Test]
        public void MapService_TickStability_Applies_FreshCapture_Growth_Bonus_When_Overclock_Active()
        {
            var game = new GameState();
            var overclockCfg = ScriptableObject.CreateInstance<OverclockConfig>();
            overclockCfg.DurationSeconds = 10;
            overclockCfg.RegenSeconds = 90;
            overclockCfg.MaxCharges = 2;
            overclockCfg.ProductionMultiplier = 3.0;
            overclockCfg.PvpAttackMultiplier = 1.2;
            overclockCfg.FreshCaptureStabilityGrowthMultiplier = 1.1;

            var overclock = new OverclockService(game, overclockCfg);
            game.Overclock.Charges = 2;

            var mapCfg = ScriptableObject.CreateInstance<MapConfig>();
            mapCfg.StabilityGrowthPerSecond = 1f;
            mapCfg.FreshCaptureWindowSeconds = 60;
            mapCfg.ValidateOrThrow();

            var mapState = new MapState
            {
                Sectors = new[]
                {
                    new SectorState
                    {
                        SectorId = 1,
                        CapturedUnixSeconds = 1000,
                        Stability = 0f
                    }
                }
            };

            var map = new MapService(mapState, mapCfg, overclock);

            map.TickStability(nowUnixSeconds: 1000); // init baseline

            Assert.IsTrue(overclock.Activate(nowUnixSeconds: 1000));
            map.TickStability(nowUnixSeconds: 1005);
            var withBoost = mapState.Sectors[0].Stability;

            mapState.Sectors[0].Stability = 0f;
            var mapNoBoost = new MapService(mapState, mapCfg, overclock: null);
            mapNoBoost.TickStability(nowUnixSeconds: 1000);
            mapNoBoost.TickStability(nowUnixSeconds: 1005);
            var withoutBoost = mapState.Sectors[0].Stability;

            Assert.Greater(withBoost, withoutBoost);
        }
    }
}
