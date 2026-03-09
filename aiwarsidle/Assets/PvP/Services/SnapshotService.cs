using System;
using System.Collections.Generic;
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
        private readonly Dictionary<int, MapConfig.SectorDefinition> _sectorDefById;
        private readonly MapConfig _mapConfig;

        public SnapshotService(
            GameState state,
            PvpConfig config,
            ProductionService production,
            int localPlayerId = DefaultLocalPlayerId,
            MapConfig mapConfig = null)
        {
            _state = state ?? throw new ArgumentNullException(nameof(state));
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _production = production ?? throw new ArgumentNullException(nameof(production));
            _localPlayerId = localPlayerId;
            _mapConfig = mapConfig;

            if (_localPlayerId <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(localPlayerId), "Local player id must be > 0.");
            }

            _config.ValidateOrThrow();

            if (_mapConfig != null)
            {
                _mapConfig.ValidateOrThrow();
                _sectorDefById = BuildSectorDefinitionMap(_mapConfig);
            }
        }

        public PvpSnapshot BuildSnapshot()
        {
            var effectiveSectorCount = CountOwnedSectors(_state.MapState, _localPlayerId, _mapConfig);

            var core =
                1.0 +
                (_state.PrestigeCount * _config.PrestigePvpPowerPerPrestige) +
                (_state.PermanentUpgradeLevel * _config.PermanentPvpPowerPerLevel) +
                (effectiveSectorCount * _config.SectorPvpPowerPerSector);

            if (core < 0) core = 0;

            if (_mapConfig != null && _sectorDefById != null && _sectorDefById.Count > 0)
            {
                ApplySectorPvpBonuses(ref core);
            }

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

        private void ApplySectorPvpBonuses(ref double core)
        {
            var sectors = _state.MapState?.Sectors;
            if (sectors == null || sectors.Length == 0) return;
            var connectedOwnedSectors = MapConnectivityService.BuildHomeConnectedSectorSet(_state.MapState, _mapConfig, _localPlayerId);

            var flat = 0.0;
            var percent = 0.0;

            for (var i = 0; i < sectors.Length; i++)
            {
                var sector = sectors[i];
                if (sector == null) continue;
                if (sector.OwnerPlayerId != _localPlayerId) continue;
                if (connectedOwnedSectors.Count > 0 && !connectedOwnedSectors.Contains(sector.SectorId)) continue;
                if (!_sectorDefById.TryGetValue(sector.SectorId, out var def) || def == null) continue;

                flat += def.PvpPowerBonusFlat;
                percent += def.PvpPowerBonusPercent;
            }

            if (flat < 0) flat = 0;
            core += flat;

            var multiplier = 1.0 + (percent / 100.0);
            if (double.IsNaN(multiplier) || double.IsInfinity(multiplier) || multiplier <= 0) return;
            core *= multiplier;
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

        private int CountOwnedSectors(MapState mapState, int localPlayerId, MapConfig mapConfig)
        {
            if (mapState?.Sectors == null || mapState.Sectors.Length == 0) return 0;
            if (ReferenceEquals(mapConfig, null)) return CountOwnedSectors(mapState, localPlayerId);

            var connectedOwnedSectors = MapConnectivityService.BuildHomeConnectedSectorSet(mapState, mapConfig, localPlayerId);
            if (connectedOwnedSectors.Count == 0) return 0;

            var count = 0;
            for (var i = 0; i < mapState.Sectors.Length; i++)
            {
                var sector = mapState.Sectors[i];
                if (sector == null) continue;
                if (sector.OwnerPlayerId != localPlayerId) continue;
                if (!connectedOwnedSectors.Contains(sector.SectorId)) continue;
                count++;
            }

            return count;
        }

        private static Dictionary<int, MapConfig.SectorDefinition> BuildSectorDefinitionMap(MapConfig config)
        {
            var map = new Dictionary<int, MapConfig.SectorDefinition>();
            if (config?.SectorDefinitions == null || config.SectorDefinitions.Length == 0) return map;

            for (var i = 0; i < config.SectorDefinitions.Length; i++)
            {
                var def = config.SectorDefinitions[i];
                if (def == null) continue;
                if (!map.ContainsKey(def.SectorId)) map.Add(def.SectorId, def);
            }

            return map;
        }
    }
}
