using System;
using AIWarsIdle.GameCore.Domain;

namespace AIWarsIdle.GameCore.Services
{
    public sealed class EconomyService
    {
        private readonly GameState _state;
        private readonly IEventBus _eventBus;

        public EconomyService(GameState state, IEventBus eventBus = null)
        {
            _state = state ?? throw new ArgumentNullException(nameof(state));
            _eventBus = eventBus;
        }

        public double Balance => _state.SoftCurrency;
        public double LifetimeEarned => _state.LifetimeEarnedSoftCurrency;

        public void AddCurrency(double amount, CurrencySource source = CurrencySource.Unknown)
        {
            ValidateFinite(amount, nameof(amount));
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount), "Amount must be >= 0.");
            if (amount == 0) return;

            _state.SoftCurrency += amount;
            _state.LifetimeEarnedSoftCurrency += amount;

            ValidateFinite(_state.SoftCurrency, nameof(_state.SoftCurrency));
            ValidateFinite(_state.LifetimeEarnedSoftCurrency, nameof(_state.LifetimeEarnedSoftCurrency));

            _eventBus?.Publish(new CurrencyChangedEvent(_state.SoftCurrency, _state.LifetimeEarnedSoftCurrency, amount, source));
        }

        public bool SpendCurrency(double amount, CurrencySource source = CurrencySource.Unknown)
        {
            ValidateFinite(amount, nameof(amount));
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount), "Amount must be >= 0.");
            if (amount == 0) return true;

            if (amount > _state.SoftCurrency)
            {
                return false;
            }

            _state.SoftCurrency -= amount;
            if (_state.SoftCurrency < 0) _state.SoftCurrency = 0;

            _eventBus?.Publish(new CurrencyChangedEvent(_state.SoftCurrency, _state.LifetimeEarnedSoftCurrency, -amount, source));
            return true;
        }

        private static void ValidateFinite(double value, string name)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(name, "Value must be finite.");
            }
        }
    }
}
