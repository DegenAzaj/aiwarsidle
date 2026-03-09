using System;
using System.Collections.Generic;
using AIWarsIdle.GameCore.Domain;
using AIWarsIdle.PvP.Config;

namespace AIWarsIdle.PvP.Services
{
    public static class MapConnectivityService
    {
        public static HashSet<int> BuildHomeConnectedSectorSet(MapState mapState, MapConfig mapConfig, int ownerPlayerId)
        {
            var connected = new HashSet<int>();
            if (ownerPlayerId <= 0) return connected;
            if (mapState?.Sectors == null || mapState.Sectors.Length == 0) return connected;
            if (ReferenceEquals(mapConfig, null)) return connected;

            var sectorsById = BuildSectorMap(mapState);
            var neighborsBySectorId = BuildNeighborMap(mapConfig);
            var homeIds = mapConfig.GetEffectiveHomeSectorIds();
            var queue = new Queue<int>();

            for (var i = 0; i < homeIds.Length; i++)
            {
                var homeId = homeIds[i];
                if (mapConfig.GetHomeOwnerPlayerId(homeId) != ownerPlayerId) continue;
                if (!sectorsById.TryGetValue(homeId, out var homeSector) || homeSector == null) continue;
                if (homeSector.OwnerPlayerId != ownerPlayerId) continue;
                if (connected.Add(homeId))
                {
                    queue.Enqueue(homeId);
                }
            }

            while (queue.Count > 0)
            {
                var sectorId = queue.Dequeue();
                if (!neighborsBySectorId.TryGetValue(sectorId, out var neighbors) || neighbors == null) continue;

                foreach (var neighborId in neighbors)
                {
                    if (connected.Contains(neighborId)) continue;
                    if (!sectorsById.TryGetValue(neighborId, out var neighborSector) || neighborSector == null) continue;
                    if (neighborSector.OwnerPlayerId != ownerPlayerId) continue;

                    connected.Add(neighborId);
                    queue.Enqueue(neighborId);
                }
            }

            return connected;
        }

        private static Dictionary<int, SectorState> BuildSectorMap(MapState mapState)
        {
            var map = new Dictionary<int, SectorState>();
            var sectors = mapState?.Sectors;
            if (sectors == null) return map;

            for (var i = 0; i < sectors.Length; i++)
            {
                var sector = sectors[i];
                if (sector == null) continue;
                if (!map.ContainsKey(sector.SectorId))
                {
                    map.Add(sector.SectorId, sector);
                }
            }

            return map;
        }

        private static Dictionary<int, HashSet<int>> BuildNeighborMap(MapConfig mapConfig)
        {
            var map = new Dictionary<int, HashSet<int>>();
            var defs = mapConfig?.SectorDefinitions ?? Array.Empty<MapConfig.SectorDefinition>();
            for (var i = 0; i < defs.Length; i++)
            {
                var def = defs[i];
                if (def == null) continue;
                if (!map.ContainsKey(def.SectorId))
                {
                    map.Add(def.SectorId, new HashSet<int>());
                }
            }

            var edges = mapConfig?.Adjacency ?? Array.Empty<MapConfig.SectorEdge>();
            for (var i = 0; i < edges.Length; i++)
            {
                var edge = edges[i];
                if (!map.TryGetValue(edge.A, out var aSet))
                {
                    aSet = new HashSet<int>();
                    map.Add(edge.A, aSet);
                }

                if (!map.TryGetValue(edge.B, out var bSet))
                {
                    bSet = new HashSet<int>();
                    map.Add(edge.B, bSet);
                }

                aSet.Add(edge.B);
                bSet.Add(edge.A);
            }

            return map;
        }
    }
}
