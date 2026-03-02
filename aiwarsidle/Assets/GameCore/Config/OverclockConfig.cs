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
        [Min(1f)]
        public float ProductionMultiplier = 3f;

        [Min(1f)]
        public float PvpAttackMultiplier = 1.2f;

        [Min(1f)]
        public float FreshCaptureStabilityGrowthMultiplier = 1.1f;

        public void ValidateOrThrow()
        {
            if (DurationSeconds <= 0) throw new InvalidOperationException($"{nameof(DurationSeconds)} must be > 0.");
            if (RegenSeconds <= 0) throw new InvalidOperationException($"{nameof(RegenSeconds)} must be > 0.");
            if (MaxCharges < 0) throw new InvalidOperationException($"{nameof(MaxCharges)} must be >= 0.");

            ValidateMultiplierOrThrow(ProductionMultiplier, nameof(ProductionMultiplier));
            ValidateMultiplierOrThrow(PvpAttackMultiplier, nameof(PvpAttackMultiplier));
            ValidateMultiplierOrThrow(FreshCaptureStabilityGrowthMultiplier, nameof(FreshCaptureStabilityGrowthMultiplier));
        }

        private static void ValidateMultiplierOrThrow(float value, string name)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                throw new InvalidOperationException($"{name} must be finite.");
            }

            if (value < 1f)
            {
                throw new InvalidOperationException($"{name} must be >= 1.");
            }
        }
    }
}

