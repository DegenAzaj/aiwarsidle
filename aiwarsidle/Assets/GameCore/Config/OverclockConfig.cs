using System;
using UnityEngine;

namespace AIWarsIdle.GameCore.Config
{
    [CreateAssetMenu(menuName = "AI Wars Idle/Overclock Config", fileName = "OverclockConfig")]
    public sealed class OverclockConfig : ScriptableObject
    {
        [Header("Timing (seconds)")]
        [Min(1)]
        public int DurationSeconds = 10;

        [Min(1)]
        public int RegenSeconds = 90;

        [Min(0)]
        public int MaxCharges = 2;

        [Header("Multipliers")]
        public double ProductionMultiplier = 3.0;

        public double PvpAttackMultiplier = 1.2;

        public double FreshCaptureStabilityGrowthMultiplier = 1.1;

        public void ValidateOrThrow()
        {
            if (DurationSeconds <= 0) throw new InvalidOperationException($"{nameof(DurationSeconds)} must be > 0.");
            if (RegenSeconds <= 0) throw new InvalidOperationException($"{nameof(RegenSeconds)} must be > 0.");
            if (MaxCharges < 0) throw new InvalidOperationException($"{nameof(MaxCharges)} must be >= 0.");

            ValidateMultiplierOrThrow(ProductionMultiplier, nameof(ProductionMultiplier));
            ValidateMultiplierOrThrow(PvpAttackMultiplier, nameof(PvpAttackMultiplier));
            ValidateMultiplierOrThrow(FreshCaptureStabilityGrowthMultiplier, nameof(FreshCaptureStabilityGrowthMultiplier));
        }

        private static void ValidateMultiplierOrThrow(double value, string name)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                throw new InvalidOperationException($"{name} must be finite.");
            }

            if (value < 1.0)
            {
                throw new InvalidOperationException($"{name} must be >= 1.");
            }
        }
    }
}
