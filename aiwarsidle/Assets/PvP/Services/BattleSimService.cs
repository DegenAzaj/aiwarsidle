using System;
using AIWarsIdle.GameCore.Domain;
using AIWarsIdle.GameCore.Services;
using AIWarsIdle.PvP.Config;

namespace AIWarsIdle.PvP.Services
{
    public sealed class BattleSimService
    {
        private readonly PvpConfig _config;
        private readonly OverclockService _overclock;

        public BattleSimService(PvpConfig config, OverclockService overclock = null)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _overclock = overclock;
            _config.ValidateOrThrow();
        }

        public BattleResult Simulate(
            double attackerPvpPower,
            double defenderPvpPower,
            AttackStrategy strategy,
            float stability,
            int seed,
            double attackerAttackMultiplier = 1.0)
        {
            if (double.IsNaN(attackerPvpPower) || double.IsInfinity(attackerPvpPower) || attackerPvpPower < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(attackerPvpPower), "Attacker power must be finite and >= 0.");
            }
            if (double.IsNaN(defenderPvpPower) || double.IsInfinity(defenderPvpPower) || defenderPvpPower < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(defenderPvpPower), "Defender power must be finite and >= 0.");
            }
            if (float.IsNaN(stability) || float.IsInfinity(stability) || stability < 0f || stability > 100f)
            {
                throw new ArgumentOutOfRangeException(nameof(stability), "Stability must be finite and in range 0..100.");
            }
            if (double.IsNaN(attackerAttackMultiplier) || double.IsInfinity(attackerAttackMultiplier) || attackerAttackMultiplier <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(attackerAttackMultiplier), "Attack multiplier must be finite and > 0.");
            }

            var rng = new Random(seed);

            var strategyMultiplier = GetStrategyMultiplier(strategy);
            var varianceA = NextUniform(rng, _config.PowerVarianceMin, _config.PowerVarianceMax);
            var varianceD = NextUniform(rng, _config.PowerVarianceMin, _config.PowerVarianceMax);

            var stabilityMultiplier = GetStabilityMultiplier(stability);

            var attackRoll = attackerPvpPower * strategyMultiplier * attackerAttackMultiplier * varianceA;
            var defenseRoll = defenderPvpPower * stabilityMultiplier * varianceD;

            return new BattleResult
            {
                AttackRoll = attackRoll,
                DefenseRoll = defenseRoll,
                Win = attackRoll > defenseRoll,
                LeaguePointsDelta = 0,
                SoftReward = 0
            };
        }

        public BattleResult Simulate(
            double attackerPvpPower,
            double defenderPvpPower,
            AttackStrategy strategy,
            float stability,
            long nowUnixSeconds,
            int seed)
        {
            if (nowUnixSeconds < 0) throw new ArgumentOutOfRangeException(nameof(nowUnixSeconds), "Timestamp must be >= 0.");
            var attackMultiplier = _overclock?.GetPvpAttackMultiplier(nowUnixSeconds) ?? 1.0;
            return Simulate(attackerPvpPower, defenderPvpPower, strategy, stability, seed, attackerAttackMultiplier: attackMultiplier);
        }

        private double GetStrategyMultiplier(AttackStrategy strategy)
        {
            return strategy switch
            {
                AttackStrategy.Aggressive => _config.StrategyMultiplierAggressive,
                AttackStrategy.Stable => _config.StrategyMultiplierStable,
                AttackStrategy.Risky => _config.StrategyMultiplierRisky,
                _ => throw new ArgumentOutOfRangeException(nameof(strategy), "Unknown strategy.")
            };
        }

        private double GetStabilityMultiplier(float stability)
        {
            var t = stability / 100f;
            return _config.StabilityMultiplierMin + ((_config.StabilityMultiplierMax - _config.StabilityMultiplierMin) * t);
        }

        private static double NextUniform(Random rng, float min, float max)
        {
            if (min == max) return min;
            var u = rng.NextDouble();
            return min + ((max - min) * u);
        }
    }
}
