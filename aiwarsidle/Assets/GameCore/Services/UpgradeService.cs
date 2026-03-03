using System;
using AIWarsIdle.GameCore.Config;
using AIWarsIdle.GameCore.Domain;

namespace AIWarsIdle.GameCore.Services
{
    public sealed class UpgradeService
    {
        private readonly GameState _state;
        private readonly BalanceConfig _config;
        private readonly EconomyService _economy;
        private readonly IEventBus _eventBus;

        public UpgradeService(GameState state, BalanceConfig config, EconomyService economy, IEventBus eventBus = null)
        {
            _state = state ?? throw new ArgumentNullException(nameof(state));
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _economy = economy ?? throw new ArgumentNullException(nameof(economy));
            _eventBus = eventBus;

            _config.ValidateOrThrow();
        }

        public double GetUpgradeCost(int generatorId)
        {
            ValidateGeneratorId(generatorId);
            var level = _state.GeneratorLevels[generatorId];
            if (level < 0) level = 0;
            return GetUpgradeCostForLevel(generatorId, level);
        }

        public int UpgradeGenerator(int generatorId)
        {
            return UpgradeGeneratorX1(generatorId);
        }

        public int UpgradeGeneratorX1(int generatorId)
        {
            return TryUpgradeInternal(generatorId, maxCount: 1);
        }

        public int UpgradeGeneratorX10(int generatorId)
        {
            return TryUpgradeInternal(generatorId, maxCount: 10);
        }

        public int UpgradeGeneratorMax(int generatorId, int maxIterations = 100_000)
        {
            if (maxIterations <= 0) throw new ArgumentOutOfRangeException(nameof(maxIterations), "Max iterations must be > 0.");
            ValidateGeneratorId(generatorId);

            var upgraded = 0;
            for (var i = 0; i < maxIterations; i++)
            {
                var cost = GetUpgradeCost(generatorId);
                if (!_economy.SpendCurrency(cost, CurrencySource.Unknown))
                {
                    break;
                }

                _state.GeneratorLevels[generatorId] = Math.Max(0, _state.GeneratorLevels[generatorId]) + 1;
                upgraded++;
            }

            if (upgraded > 0)
            {
                _eventBus?.Publish(new GeneratorUpgradedEvent(generatorId, _state.GeneratorLevels[generatorId], upgraded));
            }

            return upgraded;
        }

        private int TryUpgradeInternal(int generatorId, int maxCount)
        {
            if (maxCount <= 0) throw new ArgumentOutOfRangeException(nameof(maxCount), "Max count must be > 0.");
            ValidateGeneratorId(generatorId);

            var upgraded = 0;
            for (var i = 0; i < maxCount; i++)
            {
                var cost = GetUpgradeCost(generatorId);
                if (!_economy.SpendCurrency(cost, CurrencySource.Unknown))
                {
                    break;
                }

                _state.GeneratorLevels[generatorId] = Math.Max(0, _state.GeneratorLevels[generatorId]) + 1;
                upgraded++;
            }

            if (upgraded > 0)
            {
                _eventBus?.Publish(new GeneratorUpgradedEvent(generatorId, _state.GeneratorLevels[generatorId], upgraded));
            }

            return upgraded;
        }

        private double GetUpgradeCostForLevel(int generatorId, int level)
        {
            if (level < 0) level = 0;

            var baseCost = _config.GeneratorBaseCosts[generatorId];
            var growth = _config.GeneratorCostGrowthFactors[generatorId];
            return baseCost * Math.Pow(growth, level);
        }

        private static void ValidateGeneratorId(int generatorId)
        {
            if (generatorId < 0 || generatorId >= GameState.GeneratorCount)
            {
                throw new ArgumentOutOfRangeException(nameof(generatorId), $"GeneratorId must be in range 0..{GameState.GeneratorCount - 1}.");
            }
        }
    }
}
