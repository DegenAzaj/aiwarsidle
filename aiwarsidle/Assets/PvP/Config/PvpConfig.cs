using System;
using UnityEngine;

namespace AIWarsIdle.PvP.Config
{
    [CreateAssetMenu(menuName = "AI Wars Idle/PvP Config", fileName = "PvpConfig")]
    public sealed class PvpConfig : ScriptableObject
    {
        [Header("Randomness")]
        [Min(0.01f)]
        public float PowerVarianceMin = 0.95f;

        [Min(0.01f)]
        public float PowerVarianceMax = 1.05f;

        [Header("Strategy multipliers")]
        [Min(0.01f)]
        public float StrategyMultiplierAggressive = 1.05f;

        [Min(0.01f)]
        public float StrategyMultiplierStable = 1.00f;

        [Min(0.01f)]
        public float StrategyMultiplierRisky = 1.10f;

        [Header("Defense vs Stability")]
        [Min(0.01f)]
        public float StabilityMultiplierMin = 0.90f;

        [Min(0.01f)]
        public float StabilityMultiplierMax = 1.20f;

        public void ValidateOrThrow()
        {
            ValidateFinitePositiveOrThrow(PowerVarianceMin, nameof(PowerVarianceMin));
            ValidateFinitePositiveOrThrow(PowerVarianceMax, nameof(PowerVarianceMax));
            if (PowerVarianceMin > PowerVarianceMax)
            {
                throw new InvalidOperationException($"{nameof(PowerVarianceMin)} must be <= {nameof(PowerVarianceMax)}.");
            }

            ValidateFinitePositiveOrThrow(StrategyMultiplierAggressive, nameof(StrategyMultiplierAggressive));
            ValidateFinitePositiveOrThrow(StrategyMultiplierStable, nameof(StrategyMultiplierStable));
            ValidateFinitePositiveOrThrow(StrategyMultiplierRisky, nameof(StrategyMultiplierRisky));

            ValidateFinitePositiveOrThrow(StabilityMultiplierMin, nameof(StabilityMultiplierMin));
            ValidateFinitePositiveOrThrow(StabilityMultiplierMax, nameof(StabilityMultiplierMax));
            if (StabilityMultiplierMin > StabilityMultiplierMax)
            {
                throw new InvalidOperationException($"{nameof(StabilityMultiplierMin)} must be <= {nameof(StabilityMultiplierMax)}.");
            }
        }

        private static void ValidateFinitePositiveOrThrow(float value, string name)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                throw new InvalidOperationException($"{name} must be finite.");
            }

            if (value <= 0f)
            {
                throw new InvalidOperationException($"{name} must be > 0.");
            }
        }
    }
}

