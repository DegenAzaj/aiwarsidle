using System;
using System.Collections.Generic;
using AIWarsIdle.GameCore.Domain;
using AIWarsIdle.PvP.Config;

namespace AIWarsIdle.PvP.Services
{
    public sealed class FactionSnapshotService
    {
        private readonly GameState _state;
        private readonly MapConfig _mapConfig;
        private readonly PvpConfig _pvpConfig;
        private readonly SnapshotService _localSnapshotService;
        private readonly Dictionary<int, MapConfig.SectorDefinition> _sectorDefById;

        public FactionSnapshotService(
            GameState state,
            MapConfig mapConfig,
            PvpConfig pvpConfig,
            SnapshotService localSnapshotService = null)
        {
            _state = state ?? throw new ArgumentNullException(nameof(state));
            _mapConfig = mapConfig ?? throw new ArgumentNullException(nameof(mapConfig));
            _pvpConfig = pvpConfig ?? throw new ArgumentNullException(nameof(pvpConfig));
            _localSnapshotService = localSnapshotService;

            _mapConfig.ValidateOrThrow();
            _pvpConfig.ValidateOrThrow();
            _sectorDefById = BuildSectorDefinitionMap(_mapConfig);
        }

        public PvpSnapshot BuildCurrentSnapshot(int playerId)
        {
            if (playerId <= 0) return new PvpSnapshot();

            if (playerId == _mapConfig.LocalPlayerId && _localSnapshotService != null)
            {
                return CloneSnapshot(_localSnapshotService.BuildSnapshot());
            }

            return BuildBotSnapshot(playerId);
        }

        private PvpSnapshot BuildBotSnapshot(int playerId)
        {
            var connectedOwned = MapConnectivityService.BuildHomeConnectedSectorSet(_state.MapState, _mapConfig, playerId);
            var connectedOwnedCount = connectedOwned.Count;
            var ownerKey = Math.Max(0, playerId - _mapConfig.LocalPlayerId);

            // Independent bot power model:
            // - starts from a faction baseline,
            // - grows with home-connected territory,
            // - varies by bot profile,
            // - can still be tuned through existing bot min/max multipliers,
            // - does not mirror the current local player snapshot.
            var basePower = 55.0 + (ownerKey * 6.0) + (connectedOwnedCount * 18.0);
            basePower += connectedOwnedCount * _pvpConfig.SectorPvpPowerPerSector;

            ApplyOwnedSectorBonuses(playerId, connectedOwned, ref basePower);

            var power = basePower * GetProfileMultiplier(playerId) * GetBotConfigMultiplier(playerId);
            if (double.IsNaN(power) || double.IsInfinity(power) || power < 0) power = 0;

            return new PvpSnapshot
            {
                PvpPower = power,
                League = 0,
                SeasonPoints = 0
            };
        }

        private void ApplyOwnedSectorBonuses(int playerId, HashSet<int> connectedOwned, ref double power)
        {
            if (connectedOwned.Count == 0) return;
            if (_sectorDefById.Count == 0) return;

            var flat = 0.0;
            var percent = 0.0;
            var sectors = _state.MapState?.Sectors ?? Array.Empty<SectorState>();

            for (var i = 0; i < sectors.Length; i++)
            {
                var sector = sectors[i];
                if (sector == null) continue;
                if (sector.OwnerPlayerId != playerId) continue;
                if (!connectedOwned.Contains(sector.SectorId)) continue;
                if (!_sectorDefById.TryGetValue(sector.SectorId, out var def) || def == null) continue;

                flat += def.PvpPowerBonusFlat;
                percent += def.PvpPowerBonusPercent;
            }

            if (flat > 0) power += flat;

            var multiplier = 1.0 + (percent / 100.0);
            if (!double.IsNaN(multiplier) && !double.IsInfinity(multiplier) && multiplier > 0)
            {
                power *= multiplier;
            }
        }

        private double GetProfileMultiplier(int playerId)
        {
            return (Math.Abs(playerId) % 3) switch
            {
                0 => 1.08,
                1 => 0.96,
                _ => 1.02
            };
        }

        private double GetBotConfigMultiplier(int playerId)
        {
            var min = _mapConfig.BotPowerMinMultiplier;
            var max = _mapConfig.BotPowerMaxMultiplier;
            if (min <= 0f || max <= 0f || min > max) return 1.0;

            var bucket = Math.Abs((playerId * 97) + (_state.MapState?.MapSeasonId ?? 0)) % 1000;
            var t = bucket / 999.0;
            return min + ((max - min) * t);
        }

        private static Dictionary<int, MapConfig.SectorDefinition> BuildSectorDefinitionMap(MapConfig config)
        {
            var map = new Dictionary<int, MapConfig.SectorDefinition>();
            var defs = config?.SectorDefinitions ?? Array.Empty<MapConfig.SectorDefinition>();

            for (var i = 0; i < defs.Length; i++)
            {
                var def = defs[i];
                if (def == null) continue;
                if (!map.ContainsKey(def.SectorId))
                {
                    map.Add(def.SectorId, def);
                }
            }

            return map;
        }

        private static PvpSnapshot CloneSnapshot(PvpSnapshot src)
        {
            return src == null
                ? new PvpSnapshot()
                : new PvpSnapshot
                {
                    PvpPower = src.PvpPower,
                    League = src.League,
                    SeasonPoints = src.SeasonPoints
                };
        }
    }
}
