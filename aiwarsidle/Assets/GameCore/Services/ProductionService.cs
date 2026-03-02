using System;
using AIWarsIdle.GameCore.Config;
using AIWarsIdle.GameCore.Domain;

namespace AIWarsIdle.GameCore.Services
{
    public sealed class ProductionService
    {
        private readonly GameState _state;
        private readonly BalanceConfig _config;
        private readonly EconomyService _economy;
        private double _carrySeconds;

        public ProductionService(GameState state, BalanceConfig config, EconomyService economy)
        {
            _state = state ?? throw new ArgumentNullException(nameof(state));
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _economy = economy ?? throw new ArgumentNullException(nameof(economy));

            _config.ValidateOrThrow();
        }

        public double CalculateProductionPerSecond()
        {
            return CalculateBaseProductionPerSecondWithoutSubscription();
        }

        public double CalculateBaseProductionPerSecondWithoutSubscription()
        {
            var globalMultiplier = PrestigeMath.CalculateGlobalMultiplier(_config, _state.PermanentUpgradeLevel);

            double total = 0;
            for (var i = 0; i < GameState.GeneratorCount; i++)
            {
                var level = _state.GeneratorLevels[i];
                if (level <= 0) continue;

                var output = _config.GeneratorBaseOutputs[i] * level;
                output *= GetMilestoneMultiplier(level);
                total += output;
            }

            return total * globalMultiplier;
        }

        public double CalculateOfflineGain(double seconds)
        {
            if (double.IsNaN(seconds) || double.IsInfinity(seconds))
            {
                throw new ArgumentOutOfRangeException(nameof(seconds), "Seconds must be finite.");
            }

            if (seconds <= 0) return 0;
            if (seconds > _config.OfflineCapSeconds) seconds = _config.OfflineCapSeconds;

            return CalculateProductionPerSecond() * seconds;
        }

        public void Tick(double deltaSeconds)
        {
            if (double.IsNaN(deltaSeconds) || double.IsInfinity(deltaSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds), "Delta seconds must be finite.");
            }

            if (deltaSeconds <= 0) return;

            _carrySeconds += deltaSeconds;
            if (_carrySeconds < 1.0) return;

            var wholeSeconds = (int)Math.Floor(_carrySeconds);
            _carrySeconds -= wholeSeconds;

            for (var i = 0; i < wholeSeconds; i++)
            {
                var pps = CalculateProductionPerSecond();
                if (pps <= 0) continue;
                _economy.AddCurrency(pps, CurrencySource.OnlineProduction);
            }
        }

        private double GetMilestoneMultiplier(int level)
        {
            if (_config.MilestoneEveryLevels <= 0) return 1.0;
            if (level <= 0) return 1.0;

            var milestones = level / _config.MilestoneEveryLevels;
            if (milestones <= 0) return 1.0;

            return Math.Pow(_config.MilestoneMultiplier, milestones);
        }
    }
}

