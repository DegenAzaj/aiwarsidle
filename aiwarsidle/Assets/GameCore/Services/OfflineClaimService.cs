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

        public double PendingOfflineGain => _state.PendingOfflineGain;
        public long LastBankedOfflineRawSeconds => _state.LastBankedOfflineRawSeconds;
        public long LastBankedOfflineEffectiveSeconds => _state.LastBankedOfflineEffectiveSeconds;
        public double OfflineCapSeconds => _config.OfflineCapSeconds;

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

        public void BankOfflineGain(long nowUnixSeconds)
        {
            if (nowUnixSeconds < 0) throw new ArgumentOutOfRangeException(nameof(nowUnixSeconds), "Timestamp must be >= 0.");

            var last = _state.LastLoginUnixSeconds;
            if (last < 0) last = 0;

            var offlineSeconds = nowUnixSeconds - last;
            if (offlineSeconds <= 0)
            {
                _state.LastBankedOfflineRawSeconds = 0;
                _state.LastBankedOfflineEffectiveSeconds = 0;
                _state.LastLoginUnixSeconds = nowUnixSeconds;
                return;
            }

            _state.LastBankedOfflineRawSeconds = offlineSeconds;
            var effectiveSeconds = Math.Min((double)offlineSeconds, _config.OfflineCapSeconds);
            if (double.IsNaN(effectiveSeconds) || double.IsInfinity(effectiveSeconds) || effectiveSeconds < 0)
            {
                effectiveSeconds = 0;
            }
            _state.LastBankedOfflineEffectiveSeconds = (long)Math.Floor(effectiveSeconds);

            var add = _production.CalculateOfflineGain(offlineSeconds);
            if (add < 0) add = 0;

            var next = _state.PendingOfflineGain + add;
            if (double.IsNaN(next) || double.IsInfinity(next) || next < 0) next = 0;
            _state.PendingOfflineGain = next;
            _state.LastLoginUnixSeconds = nowUnixSeconds;
        }

        public void MarkBackgrounded(long nowUnixSeconds)
        {
            if (nowUnixSeconds < 0) throw new ArgumentOutOfRangeException(nameof(nowUnixSeconds), "Timestamp must be >= 0.");

            _state.LastLoginUnixSeconds = nowUnixSeconds;
            _state.LastBankedOfflineRawSeconds = 0;
            _state.LastBankedOfflineEffectiveSeconds = 0;
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

            if (_state.PendingOfflineGain <= 0)
            {
                _state.PendingOfflineGain = 0;
                _state.LastBankedOfflineRawSeconds = 0;
                _state.LastBankedOfflineEffectiveSeconds = 0;
                return;
            }

            var baseAmount = _state.PendingOfflineGain;
            var finalAmount = baseAmount * multiplier;
            _economy.AddCurrency(finalAmount, CurrencySource.OfflineClaim);
            _eventBus?.Publish(new OfflineClaimedEvent(baseAmount, multiplier));
            _state.PendingOfflineGain = 0;
            _state.LastBankedOfflineRawSeconds = 0;
            _state.LastBankedOfflineEffectiveSeconds = 0;
        }
    }
}
