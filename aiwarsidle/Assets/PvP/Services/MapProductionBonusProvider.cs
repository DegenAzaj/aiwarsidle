using System;
using System.Collections.Generic;
using AIWarsIdle.GameCore.Domain;
using AIWarsIdle.GameCore.Services;
using AIWarsIdle.PvP.Config;

namespace AIWarsIdle.PvP.Services
{
    public sealed class MapProductionBonusProvider : IPermanentProductionMultiplierProvider
    {
        private readonly MapState _mapState;
        private readonly MapConfig _mapConfig;
        private readonly Dictionary<int, float> _bonusBySectorId;

        public MapProductionBonusProvider(MapState mapState, MapConfig mapConfig)
        {
            _mapState = mapState ?? throw new ArgumentNullException(nameof(mapState));
            _mapConfig = mapConfig ?? throw new ArgumentNullException(nameof(mapConfig));

            _mapConfig.ValidateOrThrow();
            _bonusBySectorId = BuildBonusMap(_mapConfig);
        }

        public double GetPermanentMultiplier()
        {
            var sectors = _mapState.Sectors;
            if (sectors == null || sectors.Length == 0) return 1.0;
            var connectedOwnedSectors = MapConnectivityService.BuildHomeConnectedSectorSet(_mapState, _mapConfig, _mapConfig.LocalPlayerId);

            var ownedCount = 0;
            var rawBonusPercent = 0.0;

            for (var i = 0; i < sectors.Length; i++)
            {
                var sector = sectors[i];
                if (sector == null) continue;
                if (sector.OwnerPlayerId != _mapConfig.LocalPlayerId) continue;
                if (connectedOwnedSectors.Count > 0 && !connectedOwnedSectors.Contains(sector.SectorId)) continue;

                ownedCount++;
                if (_bonusBySectorId.TryGetValue(sector.SectorId, out var b))
                {
                    rawBonusPercent += b;
                }
            }

            if (ownedCount <= 0) return 1.0;

            var tierMult = GetDiminishingMultiplier(ownedCount);
            var bonusPercent = rawBonusPercent * tierMult;

            if (_mapConfig.SectorBonusCapPercent > 0f && bonusPercent > _mapConfig.SectorBonusCapPercent)
            {
                bonusPercent = _mapConfig.SectorBonusCapPercent;
            }

            var bonusMultiplier = 1.0 + (bonusPercent / 100.0);

            var extra = ownedCount - _mapConfig.MaintenanceFreeSectors;
            if (extra < 0) extra = 0;

            var maintenancePenaltyPercent = extra * _mapConfig.MaintenancePenaltyPercentPerExtraSector;
            var maintenanceMultiplier = 1.0 - (maintenancePenaltyPercent / 100.0);

            if (maintenanceMultiplier < _mapConfig.MaintenanceMinMultiplier) maintenanceMultiplier = _mapConfig.MaintenanceMinMultiplier;
            if (maintenanceMultiplier > 1.0) maintenanceMultiplier = 1.0;

            var total = bonusMultiplier * maintenanceMultiplier;
            if (double.IsNaN(total) || double.IsInfinity(total) || total <= 0) return 1.0;
            return total;
        }

        private double GetDiminishingMultiplier(int ownedSectorCount)
        {
            var tiers = _mapConfig.ProductionBonusDiminishingTiers;
            if (tiers == null || tiers.Length == 0) return 1.0;

            for (var i = 0; i < tiers.Length; i++)
            {
                var tier = tiers[i];
                if (tier == null) continue;
                if (ownedSectorCount <= tier.MaxOwnedSectors) return tier.BonusMultiplier;
            }

            for (var i = tiers.Length - 1; i >= 0; i--)
            {
                var tier = tiers[i];
                if (tier == null) continue;
                return tier.BonusMultiplier;
            }

            return 1.0;
        }

        private static Dictionary<int, float> BuildBonusMap(MapConfig config)
        {
            var defs = config.SectorDefinitions;
            var map = new Dictionary<int, float>();
            if (defs == null || defs.Length == 0) return map;

            for (var i = 0; i < defs.Length; i++)
            {
                var def = defs[i];
                if (def == null) continue;
                map[def.SectorId] = def.ProductionBonusPercent;
            }

            return map;
        }
    }
}
