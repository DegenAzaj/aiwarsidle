using System;
using AIWarsIdle.GameCore.Domain;
using AIWarsIdle.PvP.Config;

namespace AIWarsIdle.PvP.Services
{
    public sealed class PvpAttackChargesService
    {
        private readonly GameState _state;
        private readonly PvpAttacksConfig _config;

        public PvpAttackChargesService(GameState state, PvpAttacksConfig config)
        {
            _state = state ?? throw new ArgumentNullException(nameof(state));
            _config = config ?? throw new ArgumentNullException(nameof(config));

            _config.ValidateOrThrow();
        }

        public int MaxAttacks => _config.MaxAttacks;

        public int GetRemaining(long nowUnixSeconds)
        {
            Tick(nowUnixSeconds);
            return ClampAttacks(_state.PvpAttacksRemaining);
        }

        public long GetNextRegenAt(long nowUnixSeconds)
        {
            Tick(nowUnixSeconds);
            return _state.NextPvpAttackRegenAtUnixSeconds;
        }

        public void Tick(long nowUnixSeconds)
        {
            if (nowUnixSeconds < 0) throw new ArgumentOutOfRangeException(nameof(nowUnixSeconds), "Timestamp must be >= 0.");

            if (MaxAttacks <= 0)
            {
                _state.PvpAttacksRemaining = 0;
                _state.NextPvpAttackRegenAtUnixSeconds = 0;
                return;
            }

            _state.PvpAttacksRemaining = ClampAttacks(_state.PvpAttacksRemaining);

            if (_state.PvpAttacksRemaining >= MaxAttacks)
            {
                // No banking: if a scheduled regen is already in the past, discard it while capped.
                if (_state.NextPvpAttackRegenAtUnixSeconds > 0 && _state.NextPvpAttackRegenAtUnixSeconds <= nowUnixSeconds)
                {
                    _state.NextPvpAttackRegenAtUnixSeconds = 0;
                }

                return;
            }

            NormalizeScheduledRegen(nowUnixSeconds);

            if (_state.NextPvpAttackRegenAtUnixSeconds <= 0)
            {
                var last = _state.LastPvpAttackRegenUnixSeconds;
                if (last < 0) last = 0;

                _state.NextPvpAttackRegenAtUnixSeconds = last > 0
                    ? last + _config.RegenSeconds
                    : nowUnixSeconds + _config.RegenSeconds;
            }

            while (_state.PvpAttacksRemaining < MaxAttacks && nowUnixSeconds >= _state.NextPvpAttackRegenAtUnixSeconds)
            {
                _state.PvpAttacksRemaining++;
                _state.LastPvpAttackRegenUnixSeconds = _state.NextPvpAttackRegenAtUnixSeconds;

                if (_state.PvpAttacksRemaining >= MaxAttacks)
                {
                    _state.PvpAttacksRemaining = MaxAttacks;
                    break;
                }

                _state.NextPvpAttackRegenAtUnixSeconds += _config.RegenSeconds;
            }

            if (_state.PvpAttacksRemaining >= MaxAttacks)
            {
                if (_state.NextPvpAttackRegenAtUnixSeconds > 0 && _state.NextPvpAttackRegenAtUnixSeconds <= nowUnixSeconds)
                {
                    _state.NextPvpAttackRegenAtUnixSeconds = 0;
                }
            }
        }

        public bool TrySpendOne(long nowUnixSeconds)
        {
            Tick(nowUnixSeconds);

            if (_state.PvpAttacksRemaining <= 0)
            {
                _state.PvpAttacksRemaining = 0;
                return false;
            }

            _state.PvpAttacksRemaining--;
            if (_state.PvpAttacksRemaining < 0) _state.PvpAttacksRemaining = 0;

            if (_state.PvpAttacksRemaining < MaxAttacks &&
                (_state.NextPvpAttackRegenAtUnixSeconds <= 0 || _state.NextPvpAttackRegenAtUnixSeconds <= nowUnixSeconds))
            {
                _state.LastPvpAttackRegenUnixSeconds = nowUnixSeconds;
                _state.NextPvpAttackRegenAtUnixSeconds = nowUnixSeconds + _config.RegenSeconds;
            }

            return true;
        }

        public int AddAttacks(long nowUnixSeconds, int amount)
        {
            if (amount <= 0) return 0;

            Tick(nowUnixSeconds);

            var before = _state.PvpAttacksRemaining;
            var after = before + amount;
            if (after > MaxAttacks) after = MaxAttacks;
            if (after < 0) after = 0;

            _state.PvpAttacksRemaining = after;

            // Better UX while still "no banking":
            // - If we hit cap but the next regen was scheduled in the future, keep it so progress can resume after spending.
            // - If the next regen is in the past, discard it (handled in Tick when capped).
            if (_state.PvpAttacksRemaining >= MaxAttacks)
            {
                if (_state.NextPvpAttackRegenAtUnixSeconds > 0 && _state.NextPvpAttackRegenAtUnixSeconds <= nowUnixSeconds)
                {
                    _state.NextPvpAttackRegenAtUnixSeconds = 0;
                }
            }
            else if (_state.NextPvpAttackRegenAtUnixSeconds <= 0)
            {
                _state.LastPvpAttackRegenUnixSeconds = nowUnixSeconds;
                _state.NextPvpAttackRegenAtUnixSeconds = nowUnixSeconds + _config.RegenSeconds;
            }

            return _state.PvpAttacksRemaining - before;
        }

        public bool CanClaimDailyAdAttack(long nowUnixSeconds)
        {
            Tick(nowUnixSeconds);

            if (_config.AdExtraAttacksPerDay <= 0) return false;
            if (MaxAttacks <= 0) return false;
            if (_state.PvpAttacksRemaining >= MaxAttacks) return false;

            var last = _state.LastPvpAdAttackClaimUnixSeconds;
            if (last < 0) last = 0;
            if (last == 0) return true; // 0 means "never claimed" (default)

            var nowDay = nowUnixSeconds / 86_400L;
            var lastDay = last / 86_400L;
            return nowDay != lastDay;
        }

        public bool TryClaimDailyAdAttack(long nowUnixSeconds)
        {
            if (!CanClaimDailyAdAttack(nowUnixSeconds)) return false;

            var added = AddAttacks(nowUnixSeconds, amount: _config.AdExtraAttacksPerDay);
            if (added <= 0) return false;

            _state.LastPvpAdAttackClaimUnixSeconds = nowUnixSeconds <= 0 ? 1 : nowUnixSeconds;
            return true;
        }

        public bool CanPurchasePremiumAttacks()
        {
            return _config.PremiumExtraAttacksPerPurchase > 0 && _config.PremiumCurrencyCostPerPurchase > 0;
        }

        public bool TryPurchasePremiumAttacks(long nowUnixSeconds, int availablePremiumCurrency, out int premiumCurrencyCost)
        {
            premiumCurrencyCost = 0;

            if (availablePremiumCurrency < 0) throw new ArgumentOutOfRangeException(nameof(availablePremiumCurrency), "Premium currency must be >= 0.");
            if (!CanPurchasePremiumAttacks()) return false;

            Tick(nowUnixSeconds);
            if (_state.PvpAttacksRemaining >= MaxAttacks) return false;

            var cost = _config.PremiumCurrencyCostPerPurchase;
            if (availablePremiumCurrency < cost) return false;

            var added = AddAttacks(nowUnixSeconds, amount: _config.PremiumExtraAttacksPerPurchase);
            if (added <= 0) return false;

            premiumCurrencyCost = cost;
            return true;
        }

        private int ClampAttacks(int value)
        {
            if (value < 0) return 0;
            if (value > MaxAttacks) return MaxAttacks;
            return value;
        }

        private void NormalizeScheduledRegen(long nowUnixSeconds)
        {
            if (_state.PvpAttacksRemaining >= MaxAttacks) return;
            if (_state.NextPvpAttackRegenAtUnixSeconds <= 0) return;

            var last = _state.LastPvpAttackRegenUnixSeconds;
            if (last < 0) last = 0;

            if (last > 0)
            {
                var expectedNext = last + _config.RegenSeconds;
                if (_state.NextPvpAttackRegenAtUnixSeconds != expectedNext)
                {
                    _state.NextPvpAttackRegenAtUnixSeconds = expectedNext;
                }
                return;
            }

            var maxReasonableNext = nowUnixSeconds + _config.RegenSeconds;
            if (_state.NextPvpAttackRegenAtUnixSeconds > maxReasonableNext)
            {
                _state.NextPvpAttackRegenAtUnixSeconds = maxReasonableNext;
            }
        }
    }
}
