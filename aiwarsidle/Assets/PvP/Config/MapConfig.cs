using System;
using UnityEngine;

namespace AIWarsIdle.PvP.Config
{
    [CreateAssetMenu(menuName = "AI Wars Idle/Map Config", fileName = "MapConfig")]
    public sealed class MapConfig : ScriptableObject
    {
        [Header("Stability")]
        [Min(0f)]
        public float StabilityGrowthPerSecond = 0.5f;

        [Min(0)]
        public int FreshCaptureWindowSeconds = 300;

        public void ValidateOrThrow()
        {
            if (float.IsNaN(StabilityGrowthPerSecond) || float.IsInfinity(StabilityGrowthPerSecond) || StabilityGrowthPerSecond < 0f)
            {
                throw new InvalidOperationException($"{nameof(StabilityGrowthPerSecond)} must be finite and >= 0.");
            }
            if (FreshCaptureWindowSeconds < 0)
            {
                throw new InvalidOperationException($"{nameof(FreshCaptureWindowSeconds)} must be >= 0.");
            }
        }
    }
}

