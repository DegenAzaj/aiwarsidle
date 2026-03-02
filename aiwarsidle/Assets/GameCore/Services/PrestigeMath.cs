using System;
using AIWarsIdle.GameCore.Config;

namespace AIWarsIdle.GameCore.Services
{
    public static class PrestigeMath
    {
        public static double CalculatePrestigeThreshold(BalanceConfig config, int prestigeCount)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            if (prestigeCount < 0) throw new ArgumentOutOfRangeException(nameof(prestigeCount), "Prestige count must be >= 0.");

            return config.PrestigeThresholdBase * Math.Pow(config.PrestigeThresholdGrowthFactor, prestigeCount);
        }

        public static double CalculateGlobalMultiplier(BalanceConfig config, int permanentUpgradeLevel)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            if (permanentUpgradeLevel < 0) permanentUpgradeLevel = 0;
            if (permanentUpgradeLevel > config.PermanentUpgradeCap) permanentUpgradeLevel = config.PermanentUpgradeCap;

            return 1.0 + (permanentUpgradeLevel * config.PrestigeMultiplierIncrease);
        }
    }
}

