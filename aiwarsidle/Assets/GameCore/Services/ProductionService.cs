using System;
using AIWarsIdle.GameCore.Config;
using AIWarsIdle.GameCore.Domain;

namespace AIWarsIdle.GameCore.Services
{
    public interface IPermanentProductionMultiplierProvider
    {
        double GetPermanentMultiplier();
    }

    public sealed class ProductionService
    {
        private readonly GameState _state;
        private readonly BalanceConfig _config;
        private readonly EconomyService _economy;
        private readonly OverclockService _overclock;
        private readonly IPermanentProductionMultiplierProvider _permanentMultiplierProvider;
        private readonly ISubscriptionService _subscription;
        private double _carrySeconds;

        public ProductionService(
            GameState state,
            BalanceConfig config,
            EconomyService economy,
            OverclockService overclock = null,
            IPermanentProductionMultiplierProvider permanentMultiplierProvider = null,
            ISubscriptionService subscription = null)
        {
            _state = state ?? throw new ArgumentNullException(nameof(state));
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _economy = economy ?? throw new ArgumentNullException(nameof(economy));
            _overclock = overclock;
            _permanentMultiplierProvider = permanentMultiplierProvider;
            _subscription = subscription;

            _config.ValidateOrThrow();
        }

        public double CalculateProductionPerSecond()
        {
            var basePps = CalculateBaseProductionPerSecondWithoutSubscription();
            var subMultiplier = _subscription?.IsActive == true ? 2.0 : 1.0;
            return basePps * subMultiplier;
        }

        public double CalculateProductionPerSecond(long nowUnixSeconds)
        {
            if (nowUnixSeconds < 0) throw new ArgumentOutOfRangeException(nameof(nowUnixSeconds), "Timestamp must be >= 0.");
            var basePps = CalculateBaseProductionPerSecondWithoutSubscription();
            var multiplier = _overclock?.GetProductionMultiplier(nowUnixSeconds) ?? 1.0;
            var subMultiplier = _subscription?.IsActive == true ? 2.0 : 1.0;
            return basePps * multiplier * subMultiplier;
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

            var permanentMultiplier = _permanentMultiplierProvider?.GetPermanentMultiplier() ?? 1.0;
            if (double.IsNaN(permanentMultiplier) || double.IsInfinity(permanentMultiplier) || permanentMultiplier <= 0)
            {
                permanentMultiplier = 1.0;
            }

            return (total * globalMultiplier) * permanentMultiplier;
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
            Tick(nowUnixSeconds: 0, deltaSeconds);
        }

        public void Tick(long nowUnixSeconds, double deltaSeconds)
        {
            if (double.IsNaN(deltaSeconds) || double.IsInfinity(deltaSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds), "Delta seconds must be finite.");
            }
            if (nowUnixSeconds < 0) throw new ArgumentOutOfRangeException(nameof(nowUnixSeconds), "Timestamp must be >= 0.");

            if (deltaSeconds <= 0) return;

            _carrySeconds += deltaSeconds;
            if (_carrySeconds < 1.0) return;

            var wholeSeconds = (int)Math.Floor(_carrySeconds);
            _carrySeconds -= wholeSeconds;

            for (var i = 0; i < wholeSeconds; i++)
            {
                long tickNowUnixSeconds = nowUnixSeconds;
                if (wholeSeconds > 1 && nowUnixSeconds > 0)
                {
                    var offset = wholeSeconds - 1 - i;
                    tickNowUnixSeconds = nowUnixSeconds - offset;
                    if (tickNowUnixSeconds < 0) tickNowUnixSeconds = 0;
                }

                var pps = _overclock == null ? CalculateProductionPerSecond() : CalculateProductionPerSecond(tickNowUnixSeconds);
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
