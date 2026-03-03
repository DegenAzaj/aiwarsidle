using System;
using AIWarsIdle.GameCore.Domain;
using AIWarsIdle.GameCore.Services;
using AIWarsIdle.PvP.Config;

namespace AIWarsIdle.PvP.Services
{
    public sealed class SnapshotService
    {
        private const int DefaultLocalPlayerId = 1;

        private readonly GameState _state;
        private readonly PvpConfig _config;
        private readonly ProductionService _production;
        private readonly int _localPlayerId;

        public SnapshotService(GameState state, PvpConfig config, ProductionService production, int localPlayerId = DefaultLocalPlayerId)
        {
            _state = state ?? throw new ArgumentNullException(nameof(state));
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _production = production ?? throw new ArgumentNullException(nameof(production));
            _localPlayerId = localPlayerId;

            if (_localPlayerId <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(localPlayerId), "Local player id must be > 0.");
            }

            _config.ValidateOrThrow();
        }

        public PvpSnapshot BuildSnapshot()
        {
            var effectiveSectorCount = CountOwnedSectors(_state.MapState, _localPlayerId);

            var core =
                1.0 +
                (_state.PrestigeCount * _config.PrestigePvpPowerPerPrestige) +
                (_state.PermanentUpgradeLevel * _config.PermanentPvpPowerPerLevel) +
                (effectiveSectorCount * _config.SectorPvpPowerPerSector);

            if (core < 0) core = 0;

            var basePpsForPvp = _production.CalculateBaseProductionPerSecondWithoutSubscription();
            if (double.IsNaN(basePpsForPvp) || double.IsInfinity(basePpsForPvp) || basePpsForPvp < 0)
            {
                basePpsForPvp = 0;
            }

            var prodBonus = 0.0;
            if (basePpsForPvp > 0 && _config.ProdToPvpMaxBonus > 0)
            {
                var t = basePpsForPvp / (basePpsForPvp + _config.ProdToPvpHalfCapPps);
                prodBonus = _config.ProdToPvpMaxBonus * t;
            }

            var power = core * (1.0 + prodBonus);
            if (double.IsNaN(power) || double.IsInfinity(power) || power < 0)
            {
                power = 0;
            }

            return new PvpSnapshot
            {
                PvpPower = power,
                League = _state.League,
                SeasonPoints = _state.SeasonPoints
            };
        }

        private static int CountOwnedSectors(MapState mapState, int localPlayerId)
        {
            if (mapState?.Sectors == null || mapState.Sectors.Length == 0) return 0;

            var count = 0;
            for (var i = 0; i < mapState.Sectors.Length; i++)
            {
                var sector = mapState.Sectors[i];
                if (sector == null) continue;
                if (sector.OwnerPlayerId == localPlayerId) count++;
            }

            return count;
        }
    }
}

