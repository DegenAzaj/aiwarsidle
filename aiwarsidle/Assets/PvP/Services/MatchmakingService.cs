using System;
using System.Collections.Generic;
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

        public PvpSnapshot GetDefenderSnapshot(PvpSnapshot attackerSnapshot, SectorState defenderSector, int seed, int attackerPlayerId = 0)
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
                ? GenerateSnapshot(
                    attackerSnapshot,
                    seed,
                    GetNeutralPowerMultiplierForDistance(_mapConfig.NeutralPowerMinMultiplier, attackerPlayerId, defenderSector.SectorId),
                    GetNeutralPowerMultiplierForDistance(_mapConfig.NeutralPowerMaxMultiplier, attackerPlayerId, defenderSector.SectorId))
                : GenerateSnapshot(attackerSnapshot, seed, _mapConfig.BotPowerMinMultiplier, _mapConfig.BotPowerMaxMultiplier);
        }

        private float GetNeutralPowerMultiplierForDistance(float baseMultiplier, int attackerPlayerId, int defenderSectorId)
        {
            if (baseMultiplier <= 0f) return baseMultiplier;

            var distanceFromHome = GetDistanceFromOwnedHome(attackerPlayerId, defenderSectorId);
            if (distanceFromHome <= 0) return baseMultiplier;

            var factor = _mapConfig.NeutralPowerHomeDistanceMinFactor +
                         ((distanceFromHome - 1) * _mapConfig.NeutralPowerHomeDistanceStepFactor);
            factor = Math.Clamp(factor, _mapConfig.NeutralPowerHomeDistanceMinFactor, 1f);
            return baseMultiplier * factor;
        }

        private int GetDistanceFromOwnedHome(int attackerPlayerId, int targetSectorId)
        {
            if (attackerPlayerId <= 0) return -1;

            var homeIds = _mapConfig.GetEffectiveHomeSectorIds();
            var queue = new Queue<int>();
            var distanceBySectorId = new Dictionary<int, int>();

            for (var i = 0; i < homeIds.Length; i++)
            {
                var homeId = homeIds[i];
                if (_mapConfig.GetHomeOwnerPlayerId(homeId) != attackerPlayerId) continue;
                if (distanceBySectorId.ContainsKey(homeId)) continue;

                distanceBySectorId.Add(homeId, 0);
                queue.Enqueue(homeId);
            }

            while (queue.Count > 0)
            {
                var sectorId = queue.Dequeue();
                if (sectorId == targetSectorId) return distanceBySectorId[sectorId];
                if (!TryGetNeighbors(sectorId, out var neighbors)) continue;

                for (var i = 0; i < neighbors.Count; i++)
                {
                    var neighborId = neighbors[i];
                    if (distanceBySectorId.ContainsKey(neighborId)) continue;

                    distanceBySectorId.Add(neighborId, distanceBySectorId[sectorId] + 1);
                    queue.Enqueue(neighborId);
                }
            }

            return -1;
        }

        private bool TryGetNeighbors(int sectorId, out List<int> neighbors)
        {
            neighbors = null;
            var edges = _mapConfig.Adjacency;
            if (edges == null || edges.Length == 0) return false;

            neighbors = new List<int>();
            for (var i = 0; i < edges.Length; i++)
            {
                var edge = edges[i];
                if (edge.A == sectorId)
                {
                    neighbors.Add(edge.B);
                }
                else if (edge.B == sectorId)
                {
                    neighbors.Add(edge.A);
                }
            }

            return neighbors.Count > 0;
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
