using System;
using AIWarsIdle.GameCore.Config;
using AIWarsIdle.GameCore.Domain;

namespace AIWarsIdle.GameCore.Services
{
    public sealed class PrestigeService
    {
        private readonly GameState _state;
        private readonly BalanceConfig _config;

        public PrestigeService(GameState state, BalanceConfig config)
        {
            _state = state ?? throw new ArgumentNullException(nameof(state));
            _config = config ?? throw new ArgumentNullException(nameof(config));

            _config.ValidateOrThrow();
        }

        public double GetCurrentGlobalMultiplier()
        {
            return PrestigeMath.CalculateGlobalMultiplier(_config, _state.PermanentUpgradeLevel);
        }

        public double GetPrestigeThreshold()
        {
            return PrestigeMath.CalculatePrestigeThreshold(_config, _state.PrestigeCount);
        }

        public bool CanPrestige()
        {
            return _state.LifetimeEarnedSoftCurrency >= GetPrestigeThreshold();
        }

        public void ExecutePrestige()
        {
            if (!CanPrestige())
            {
                throw new InvalidOperationException("Cannot prestige yet.");
            }

            _state.SoftCurrency = 0;
            for (var i = 0; i < _state.GeneratorLevels.Length; i++)
            {
                _state.GeneratorLevels[i] = 0;
            }

            _state.PrestigeCount = Math.Max(0, _state.PrestigeCount) + 1;

            var nextPermanent = Math.Max(0, _state.PermanentUpgradeLevel) + 1;
            if (nextPermanent > _config.PermanentUpgradeCap) nextPermanent = _config.PermanentUpgradeCap;
            _state.PermanentUpgradeLevel = nextPermanent;
        }
    }
}

