using System;
using AIWarsIdle.GameCore.Config;
using AIWarsIdle.GameCore.Domain;

namespace AIWarsIdle.GameCore.Services
{
    public sealed class PrestigeService
    {
        private readonly GameState _state;
        private readonly BalanceConfig _config;
        private readonly IEventBus _eventBus;

        public PrestigeService(GameState state, BalanceConfig config, IEventBus eventBus = null)
        {
            _state = state ?? throw new ArgumentNullException(nameof(state));
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _eventBus = eventBus;

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
            var earnedSinceLast = GetEarnedSinceLastPrestige();
            return earnedSinceLast >= GetPrestigeThreshold();
        }

        public int PreviewPrestigeGains(bool max = true)
        {
            var earnedSinceLast = GetEarnedSinceLastPrestige();
            if (earnedSinceLast <= 0) return 0;

            if (!max)
            {
                var threshold = GetPrestigeThreshold();
                return earnedSinceLast >= threshold ? 1 : 0;
            }

            return CalculatePrestigeGains(earnedSinceLast, out _);
        }

        public void ExecutePrestige()
        {
            if (!CanPrestige())
            {
                throw new InvalidOperationException("Cannot prestige yet.");
            }

            // Apply as many prestiges as the player can afford from the earned-since-last-prestige pool,
            // and keep the remaining progress towards the next prestige.
            var earnedSinceLast = GetEarnedSinceLastPrestige();
            var gained = CalculatePrestigeGains(earnedSinceLast, out var remainingEarned);
            if (gained <= 0)
            {
                throw new InvalidOperationException("Cannot prestige yet.");
            }

            var newBaseline = _state.LifetimeEarnedSoftCurrency - remainingEarned;
            if (newBaseline < 0) newBaseline = 0;
            if (newBaseline > _state.LifetimeEarnedSoftCurrency) newBaseline = _state.LifetimeEarnedSoftCurrency;
            _state.LifetimeEarnedSoftCurrencyAtLastPrestige = newBaseline;

            _state.SoftCurrency = 0;
            for (var i = 0; i < _state.GeneratorLevels.Length; i++)
            {
                _state.GeneratorLevels[i] = 0;
            }

            // Avoid a dead-start after prestige (no currency + no production).
            if (_state.GeneratorLevels.Length > 0)
            {
                _state.GeneratorLevels[0] = 1;
            }

            _state.PrestigeCount = Math.Max(0, _state.PrestigeCount) + gained;

            var nextPermanent = Math.Max(0, _state.PermanentUpgradeLevel) + gained;
            if (nextPermanent > _config.PermanentUpgradeCap) nextPermanent = _config.PermanentUpgradeCap;
            _state.PermanentUpgradeLevel = nextPermanent;

            _eventBus?.Publish(new PrestigeExecutedEvent(_state.PrestigeCount, _state.PermanentUpgradeLevel));
        }

        public void ExecutePrestigeSingle()
        {
            var earnedSinceLast = GetEarnedSinceLastPrestige();
            var threshold = GetPrestigeThreshold();
            if (earnedSinceLast < threshold)
            {
                throw new InvalidOperationException("Cannot prestige yet.");
            }

            var remainingEarned = earnedSinceLast - threshold;
            if (remainingEarned < 0) remainingEarned = 0;

            var newBaseline = _state.LifetimeEarnedSoftCurrency - remainingEarned;
            if (newBaseline < 0) newBaseline = 0;
            if (newBaseline > _state.LifetimeEarnedSoftCurrency) newBaseline = _state.LifetimeEarnedSoftCurrency;
            _state.LifetimeEarnedSoftCurrencyAtLastPrestige = newBaseline;

            _state.SoftCurrency = 0;
            for (var i = 0; i < _state.GeneratorLevels.Length; i++)
            {
                _state.GeneratorLevels[i] = 0;
            }

            if (_state.GeneratorLevels.Length > 0)
            {
                _state.GeneratorLevels[0] = 1;
            }

            _state.PrestigeCount = Math.Max(0, _state.PrestigeCount) + 1;

            var nextPermanent = Math.Max(0, _state.PermanentUpgradeLevel) + 1;
            if (nextPermanent > _config.PermanentUpgradeCap) nextPermanent = _config.PermanentUpgradeCap;
            _state.PermanentUpgradeLevel = nextPermanent;

            _eventBus?.Publish(new PrestigeExecutedEvent(_state.PrestigeCount, _state.PermanentUpgradeLevel));
        }

        private double GetEarnedSinceLastPrestige()
        {
            var baseline = _state.LifetimeEarnedSoftCurrencyAtLastPrestige;
            if (baseline < 0) baseline = 0;
            if (baseline > _state.LifetimeEarnedSoftCurrency) baseline = _state.LifetimeEarnedSoftCurrency;
            var earned = _state.LifetimeEarnedSoftCurrency - baseline;
            return earned < 0 ? 0 : earned;
        }

        private int CalculatePrestigeGains(double earnedSinceLastPrestige, out double remainingEarned)
        {
            remainingEarned = earnedSinceLastPrestige;
            if (double.IsNaN(remainingEarned) || double.IsInfinity(remainingEarned)) return 0;
            if (remainingEarned <= 0) return 0;

            var prestigeCount = _state.PrestigeCount;
            if (prestigeCount < 0) prestigeCount = 0;

            var gained = 0;

            // Threshold grows geometrically, so the number of iterations is small in practice.
            // Still, guard against pathological configs/values.
            const int maxIterations = 10_000;
            for (var i = 0; i < maxIterations; i++)
            {
                var threshold = PrestigeMath.CalculatePrestigeThreshold(_config, prestigeCount + gained);
                if (threshold <= 0) break;
                if (remainingEarned < threshold) break;

                remainingEarned -= threshold;
                gained++;
            }

            if (remainingEarned < 0) remainingEarned = 0;
            return gained;
        }
    }
}
