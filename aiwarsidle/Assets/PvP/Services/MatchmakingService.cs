using System;
using AIWarsIdle.GameCore.Domain;
using AIWarsIdle.PvP.Config;

namespace AIWarsIdle.PvP.Services
{
    public sealed class MatchmakingService
    {
        private readonly MapConfig _mapConfig;

        public MatchmakingService(MapConfig mapConfig)
        {
            _mapConfig = mapConfig ?? throw new ArgumentNullException(nameof(mapConfig));
            _mapConfig.ValidateOrThrow();
        }

        public PvpSnapshot GetDefenderSnapshot(PvpSnapshot attackerSnapshot, SectorState defenderSector, int seed)
        {
            if (attackerSnapshot == null) throw new ArgumentNullException(nameof(attackerSnapshot));
            if (defenderSector == null) throw new ArgumentNullException(nameof(defenderSector));

            var owner = defenderSector.OwnerSnapshot;
            if (defenderSector.OwnerPlayerId != 0 && owner != null && owner.PvpPower > 0)
            {
                return CloneSnapshot(owner);
            }

            return GenerateBotSnapshot(attackerSnapshot, seed);
        }

        private PvpSnapshot GenerateBotSnapshot(PvpSnapshot attackerSnapshot, int seed)
        {
            var rng = new Random(seed);

            var min = _mapConfig.BotPowerMinMultiplier;
            var max = _mapConfig.BotPowerMaxMultiplier;
            var u = rng.NextDouble();
            var mult = min + ((max - min) * u);

            var power = attackerSnapshot.PvpPower * mult;
            if (double.IsNaN(power) || double.IsInfinity(power) || power < 0) power = 0;

            return new PvpSnapshot
            {
                PvpPower = power,
                League = 0,
                SeasonPoints = 0
            };
        }

        private static PvpSnapshot CloneSnapshot(PvpSnapshot src)
        {
            return new PvpSnapshot
            {
                PvpPower = src.PvpPower,
                League = src.League,
                SeasonPoints = src.SeasonPoints
            };
        }
    }
}

