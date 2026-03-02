using System;
using AIWarsIdle.GameCore.Domain;
using UnityEngine;

namespace AIWarsIdle.GameCore.Config
{
    [CreateAssetMenu(menuName = "AI Wars Idle/Balance Config", fileName = "BalanceConfig")]
    public sealed class BalanceConfig : ScriptableObject
    {
        [Header("Generators")]
        public double[] GeneratorBaseCosts = new double[GameState.GeneratorCount];
        public double[] GeneratorCostGrowthFactors = new double[GameState.GeneratorCount];
        public double[] GeneratorBaseOutputs = new double[GameState.GeneratorCount];
        public int MilestoneEveryLevels = 25;
        public double MilestoneMultiplier = 2.0;

        [Header("Prestige")]
        public double PrestigeThresholdBase = 1_000;
        public double PrestigeThresholdGrowthFactor = 1.6;
        public int PermanentUpgradeCap = 10;
        public double PrestigeMultiplierIncrease = 0.05;

        [Header("Offline")]
        public double OfflineCapSeconds = 12 * 60 * 60; // 12h

        public void ValidateOrThrow()
        {
            ValidateArrayOrThrow(GeneratorBaseCosts, nameof(GeneratorBaseCosts));
            ValidateArrayOrThrow(GeneratorCostGrowthFactors, nameof(GeneratorCostGrowthFactors));
            ValidateArrayOrThrow(GeneratorBaseOutputs, nameof(GeneratorBaseOutputs));

            for (var i = 0; i < GameState.GeneratorCount; i++)
            {
                ValidateFinitePositiveOrThrow(GeneratorBaseCosts[i], $"{nameof(GeneratorBaseCosts)}[{i}]");
                ValidateFinitePositiveOrThrow(GeneratorBaseOutputs[i], $"{nameof(GeneratorBaseOutputs)}[{i}]");

                var growth = GeneratorCostGrowthFactors[i];
                ValidateFiniteOrThrow(growth, $"{nameof(GeneratorCostGrowthFactors)}[{i}]");
                if (growth <= 1.0)
                {
                    throw new InvalidOperationException($"{nameof(GeneratorCostGrowthFactors)}[{i}] must be > 1.");
                }
            }

            if (MilestoneEveryLevels < 0)
            {
                throw new InvalidOperationException($"{nameof(MilestoneEveryLevels)} must be >= 0.");
            }
            if (MilestoneEveryLevels > 0)
            {
                ValidateFiniteOrThrow(MilestoneMultiplier, nameof(MilestoneMultiplier));
                if (MilestoneMultiplier <= 1.0)
                {
                    throw new InvalidOperationException($"{nameof(MilestoneMultiplier)} must be > 1 when milestones are enabled.");
                }
            }

            ValidateFinitePositiveOrThrow(PrestigeThresholdBase, nameof(PrestigeThresholdBase));
            ValidateFiniteOrThrow(PrestigeThresholdGrowthFactor, nameof(PrestigeThresholdGrowthFactor));
            if (PrestigeThresholdGrowthFactor <= 1.0)
            {
                throw new InvalidOperationException($"{nameof(PrestigeThresholdGrowthFactor)} must be > 1.");
            }

            if (PermanentUpgradeCap < 0)
            {
                throw new InvalidOperationException($"{nameof(PermanentUpgradeCap)} must be >= 0.");
            }

            ValidateFiniteOrThrow(PrestigeMultiplierIncrease, nameof(PrestigeMultiplierIncrease));
            if (PrestigeMultiplierIncrease < 0)
            {
                throw new InvalidOperationException($"{nameof(PrestigeMultiplierIncrease)} must be >= 0.");
            }

            ValidateFinitePositiveOrThrow(OfflineCapSeconds, nameof(OfflineCapSeconds));
        }

        private static void ValidateArrayOrThrow(Array array, string name)
        {
            if (array == null) throw new InvalidOperationException($"{name} must not be null.");
            if (array.Length != GameState.GeneratorCount)
            {
                throw new InvalidOperationException($"{name} length must be {GameState.GeneratorCount}.");
            }
        }

        private static void ValidateFinitePositiveOrThrow(double value, string name)
        {
            ValidateFiniteOrThrow(value, name);
            if (value <= 0)
            {
                throw new InvalidOperationException($"{name} must be > 0.");
            }
        }

        private static void ValidateFiniteOrThrow(double value, string name)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                throw new InvalidOperationException($"{name} must be finite.");
            }
        }
    }
}
