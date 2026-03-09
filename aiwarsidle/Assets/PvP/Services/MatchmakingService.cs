using System;
using AIWarsIdle.GameCore.Domain;
using AIWarsIdle.PvP.Config;

namespace AIWarsIdle.PvP.Services
{
    public sealed class MatchmakingService
    {
        private readonly MapConfig _mapConfig;
        private readonly FactionSnapshotService _factionSnapshots;

        public MatchmakingService(MapConfig mapConfig, FactionSnapshotService factionSnapshots = null)
        {
            _mapConfig = mapConfig ?? throw new ArgumentNullException(nameof(mapConfig));
            _factionSnapshots = factionSnapshots;
            _mapConfig.ValidateOrThrow();
        }

        public PvpSnapshot GetDefenderSnapshot(PvpSnapshot attackerSnapshot, SectorState defenderSector, int seed)
        {
            if (attackerSnapshot == null) throw new ArgumentNullException(nameof(attackerSnapshot));
            if (defenderSector == null) throw new ArgumentNullException(nameof(defenderSector));

            if (defenderSector.OwnerPlayerId != 0 && _factionSnapshots != null)
            {
                return _factionSnapshots.BuildCurrentSnapshot(defenderSector.OwnerPlayerId);
            }

            var owner = defenderSector.OwnerSnapshot;
            if (defenderSector.OwnerPlayerId != 0 && owner != null && owner.PvpPower > 0)
            {
                return CloneSnapshot(owner);
            }

            return defenderSector.OwnerPlayerId == 0
                ? GenerateSnapshot(attackerSnapshot, seed, _mapConfig.NeutralPowerMinMultiplier, _mapConfig.NeutralPowerMaxMultiplier)
                : GenerateSnapshot(attackerSnapshot, seed, _mapConfig.BotPowerMinMultiplier, _mapConfig.BotPowerMaxMultiplier);
        }

        private PvpSnapshot GenerateSnapshot(PvpSnapshot attackerSnapshot, int seed, float min, float max)
        {
            var rng = new Random(seed);

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
