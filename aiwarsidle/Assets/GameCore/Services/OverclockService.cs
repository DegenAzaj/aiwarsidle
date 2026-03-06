using System;
using AIWarsIdle.GameCore.Config;
using AIWarsIdle.GameCore.Domain;
using AIWarsIdle.GameCore.Validation;

namespace AIWarsIdle.GameCore.Services
{
    public sealed class OverclockService
    {
        private readonly GameState _state;
        private readonly OverclockConfig _config;
        private readonly IEventBus _eventBus;

        public OverclockService(GameState state, OverclockConfig config, IEventBus eventBus = null)
        {
            _state = state ?? throw new ArgumentNullException(nameof(state));
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _eventBus = eventBus;

            _config.ValidateOrThrow();
            if (_state.Overclock == null) _state.Overclock = new OverclockState();

            NormalizeAndValidateState();
        }

        public int MaxCharges => _config.MaxCharges;
        public int DurationSeconds => _config.DurationSeconds;
        public int RegenSeconds => _config.RegenSeconds;

        public bool IsActive(long nowUnixSeconds)
        {
            if (nowUnixSeconds < 0) throw new ArgumentOutOfRangeException(nameof(nowUnixSeconds), "Timestamp must be >= 0.");
            return nowUnixSeconds < _state.Overclock.ActiveUntilUnixSeconds;
        }

        public bool CanActivate(long nowUnixSeconds)
        {
            if (nowUnixSeconds < 0) throw new ArgumentOutOfRangeException(nameof(nowUnixSeconds), "Timestamp must be >= 0.");
            if (nowUnixSeconds < _state.Overclock.ActiveUntilUnixSeconds) return false;

            var charges = GetChargesWithoutMutating(nowUnixSeconds);
            return charges > 0;
        }

        public bool Activate(long nowUnixSeconds)
        {
            if (nowUnixSeconds < 0) throw new ArgumentOutOfRangeException(nameof(nowUnixSeconds), "Timestamp must be >= 0.");

            Tick(nowUnixSeconds);

            if (nowUnixSeconds < _state.Overclock.ActiveUntilUnixSeconds) return false;
            if (_state.Overclock.Charges <= 0) return false;

            _state.Overclock.Charges--;
            _eventBus?.Publish(new OverclockChargeSpentEvent(nowUnixSeconds, _state.Overclock.Charges));

            _state.Overclock.ActiveUntilUnixSeconds = nowUnixSeconds + _config.DurationSeconds;

            if (_state.Overclock.Charges < MaxCharges && _state.Overclock.NextChargeAtUnixSeconds <= 0)
            {
                _state.Overclock.NextChargeAtUnixSeconds = nowUnixSeconds + _config.RegenSeconds;
            }

            _eventBus?.Publish(new OverclockActivatedEvent(nowUnixSeconds, _state.Overclock.ActiveUntilUnixSeconds, _state.Overclock.Charges));
            NormalizeAndValidateState();
            return true;
        }

        public void Tick(long nowUnixSeconds)
        {
            if (nowUnixSeconds < 0) throw new ArgumentOutOfRangeException(nameof(nowUnixSeconds), "Timestamp must be >= 0.");

            if (_state.Overclock.ActiveUntilUnixSeconds > 0 && nowUnixSeconds >= _state.Overclock.ActiveUntilUnixSeconds)
            {
                _state.Overclock.ActiveUntilUnixSeconds = 0;
            }

            if (MaxCharges <= 0)
            {
                _state.Overclock.Charges = 0;
                _state.Overclock.ActiveUntilUnixSeconds = 0;
                _state.Overclock.NextChargeAtUnixSeconds = 0;
                return;
            }

            if (_state.Overclock.Charges > MaxCharges) _state.Overclock.Charges = MaxCharges;
            if (_state.Overclock.Charges < 0) _state.Overclock.Charges = 0;

            if (_state.Overclock.Charges >= MaxCharges)
            {
                _state.Overclock.NextChargeAtUnixSeconds = 0;
                NormalizeAndValidateState();
                return;
            }

            if (_state.Overclock.NextChargeAtUnixSeconds <= 0)
            {
                _state.Overclock.NextChargeAtUnixSeconds = nowUnixSeconds + _config.RegenSeconds;
            }

            while (_state.Overclock.Charges < MaxCharges && nowUnixSeconds >= _state.Overclock.NextChargeAtUnixSeconds)
            {
                _state.Overclock.Charges++;
                _eventBus?.Publish(new OverclockChargeGainedEvent(nowUnixSeconds, _state.Overclock.Charges));

                if (_state.Overclock.Charges >= MaxCharges)
                {
                    _state.Overclock.NextChargeAtUnixSeconds = 0;
                    break;
                }

                _state.Overclock.NextChargeAtUnixSeconds += _config.RegenSeconds;
            }

            NormalizeAndValidateState();
        }

        public double GetProductionMultiplier(long nowUnixSeconds)
        {
            return IsActive(nowUnixSeconds) ? _config.ProductionMultiplier : 1.0;
        }

        public double GetPvpAttackMultiplier(long nowUnixSeconds)
        {
            return IsActive(nowUnixSeconds) ? _config.PvpAttackMultiplier : 1.0;
        }

        public double GetFreshCaptureStabilityGrowthMultiplier(long nowUnixSeconds)
        {
            return IsActive(nowUnixSeconds) ? _config.FreshCaptureStabilityGrowthMultiplier : 1.0;
        }

        private int GetChargesWithoutMutating(long nowUnixSeconds)
        {
            var charges = _state.Overclock.Charges;
            if (charges < 0) charges = 0;
            if (charges > MaxCharges) charges = MaxCharges;
            if (charges >= MaxCharges) return charges;

            var nextAt = _state.Overclock.NextChargeAtUnixSeconds;
            if (nextAt <= 0) nextAt = nowUnixSeconds + _config.RegenSeconds;
            if (nowUnixSeconds < nextAt) return charges;

            var gained = 1 + (int)((nowUnixSeconds - nextAt) / _config.RegenSeconds);
            var result = charges + gained;
            if (result > MaxCharges) result = MaxCharges;
            return result;
        }

        private void NormalizeAndValidateState()
        {
            if (_state.Overclock.ActiveUntilUnixSeconds < 0) _state.Overclock.ActiveUntilUnixSeconds = 0;
            if (_state.Overclock.NextChargeAtUnixSeconds < 0) _state.Overclock.NextChargeAtUnixSeconds = 0;

            if (MaxCharges > 0)
            {
                if (_state.Overclock.Charges < 0) _state.Overclock.Charges = 0;
                if (_state.Overclock.Charges > MaxCharges) _state.Overclock.Charges = MaxCharges;
            }
            else
            {
                _state.Overclock.Charges = 0;
                _state.Overclock.ActiveUntilUnixSeconds = 0;
                _state.Overclock.NextChargeAtUnixSeconds = 0;
            }

            DomainValidation.ValidateOverclockState(_state.Overclock, maxCharges: Math.Max(0, MaxCharges));
        }
    }
}
