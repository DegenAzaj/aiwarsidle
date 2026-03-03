using System;
using AIWarsIdle.GameCore.Config;
using AIWarsIdle.GameCore.Domain;

namespace AIWarsIdle.GameCore.Services
{
    public sealed class OfflineClaimService
    {
        private readonly GameState _state;
        private readonly BalanceConfig _config;
        private readonly ProductionService _production;
        private readonly EconomyService _economy;
        private readonly IEventBus _eventBus;

        public double PendingOfflineGain { get; private set; }

        public OfflineClaimService(
            GameState state,
            BalanceConfig config,
            ProductionService production,
            EconomyService economy,
            IEventBus eventBus = null)
        {
            _state = state ?? throw new ArgumentNullException(nameof(state));
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _production = production ?? throw new ArgumentNullException(nameof(production));
            _economy = economy ?? throw new ArgumentNullException(nameof(economy));
            _eventBus = eventBus;

            _config.ValidateOrThrow();
        }

        public void RecalculatePending(long nowUnixSeconds)
        {
            if (nowUnixSeconds < 0) throw new ArgumentOutOfRangeException(nameof(nowUnixSeconds), "Timestamp must be >= 0.");

            var last = _state.LastLoginUnixSeconds;
            if (last < 0) last = 0;

            var offlineSeconds = nowUnixSeconds - last;
            if (offlineSeconds <= 0)
            {
                PendingOfflineGain = 0;
                return;
            }

            PendingOfflineGain = _production.CalculateOfflineGain(offlineSeconds);
            if (PendingOfflineGain < 0) PendingOfflineGain = 0;
        }

        public void Claim(double multiplier)
        {
            if (double.IsNaN(multiplier) || double.IsInfinity(multiplier))
            {
                throw new ArgumentOutOfRangeException(nameof(multiplier), "Multiplier must be finite.");
            }
            if (multiplier <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(multiplier), "Multiplier must be > 0.");
            }

            if (PendingOfflineGain <= 0)
            {
                PendingOfflineGain = 0;
                return;
            }

            var baseAmount = PendingOfflineGain;
            var finalAmount = baseAmount * multiplier;
            _economy.AddCurrency(finalAmount, CurrencySource.OfflineClaim);
            _eventBus?.Publish(new OfflineClaimedEvent(baseAmount, multiplier));
            PendingOfflineGain = 0;
        }

        public void CommitLoginTimestamp(long nowUnixSeconds)
        {
            if (nowUnixSeconds < 0) throw new ArgumentOutOfRangeException(nameof(nowUnixSeconds), "Timestamp must be >= 0.");
            _state.LastLoginUnixSeconds = nowUnixSeconds;
        }
    }
}
