using System;
using System.Collections.Generic;
using AIWarsIdle.PvP.Config;
using UnityEngine;

namespace AIWarsIdle.PvP.Services
{
    public readonly struct HexGridCoord : IEquatable<HexGridCoord>
    {
        public int Q { get; }
        public int R { get; }

        public HexGridCoord(int q, int r)
        {
            Q = q;
            R = r;
        }

        public bool Equals(HexGridCoord other)
        {
            return Q == other.Q && R == other.R;
        }

        public override bool Equals(object obj)
        {
            return obj is HexGridCoord other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (Q * 397) ^ R;
            }
        }
    }

    public static class HexGridTopology
    {
        private static readonly HexGridCoord[] NeighborDirections =
        {
            new(1, 0),
            new(1, -1),
            new(0, -1),
            new(-1, 0),
            new(-1, 1),
            new(0, 1),
        };

        public static List<HexGridCoord> EnumerateAxial(int radius)
        {
            radius = Mathf.Max(0, radius);
            var coords = new List<HexGridCoord>(1 + (3 * radius * (radius + 1)));

            for (var q = -radius; q <= radius; q++)
            {
                var rMin = Mathf.Max(-radius, -q - radius);
                var rMax = Mathf.Min(radius, -q + radius);

                for (var r = rMin; r <= rMax; r++)
                {
                    coords.Add(new HexGridCoord(q, r));
                }
            }

            coords.Sort((a, b) =>
            {
                var byR = b.R.CompareTo(a.R);
                return byR != 0 ? byR : a.Q.CompareTo(b.Q);
            });

            return coords;
        }

        public static int ComputeCellCount(int radius)
        {
            radius = Mathf.Max(0, radius);
            return 1 + (3 * radius * (radius + 1));
        }

        public static bool TryInferRadiusFromCellCount(int cellCount, out int radius)
        {
            radius = 0;
            if (cellCount <= 0) return false;

            for (var r = 0; r <= 64; r++)
            {
                var expected = ComputeCellCount(r);
                if (expected == cellCount)
                {
                    radius = r;
                    return true;
                }

                if (expected > cellCount) return false;
            }

            return false;
        }

        public static List<HexGridCoord> GetCornerCoords(int radius)
        {
            radius = Mathf.Max(0, radius);
            return new List<HexGridCoord>
            {
                new(radius, 0),
                new(0, radius),
                new(-radius, radius),
                new(-radius, 0),
                new(0, -radius),
                new(radius, -radius),
            };
        }

        public static MapConfig.SectorDefinition[] BuildSectorDefinitions(int radius, float productionBonusPercent = 5f)
        {
            var coords = EnumerateAxial(radius);
            var defs = new MapConfig.SectorDefinition[coords.Count];

            for (var i = 0; i < coords.Count; i++)
            {
                defs[i] = new MapConfig.SectorDefinition
                {
                    SectorId = i,
                    Name = $"Hex {coords[i].Q},{coords[i].R}",
                    ProductionBonusPercent = productionBonusPercent,
                    PvpPowerBonusFlat = 0.0,
                    PvpPowerBonusPercent = 0f
                };
            }

            return defs;
        }

        public static MapConfig.SectorEdge[] BuildAdjacency(int radius)
        {
            var coords = EnumerateAxial(radius);
            var byCoord = new Dictionary<HexGridCoord, int>(coords.Count);
            for (var i = 0; i < coords.Count; i++)
            {
                byCoord[coords[i]] = i;
            }

            var edges = new List<MapConfig.SectorEdge>(coords.Count * 3);
            for (var i = 0; i < coords.Count; i++)
            {
                var coord = coords[i];
                for (var d = 0; d < NeighborDirections.Length; d++)
                {
                    var neighbor = new HexGridCoord(coord.Q + NeighborDirections[d].Q, coord.R + NeighborDirections[d].R);
                    if (!byCoord.TryGetValue(neighbor, out var neighborId)) continue;
                    if (neighborId <= i) continue;

                    edges.Add(new MapConfig.SectorEdge { A = i, B = neighborId });
                }
            }

            return edges.ToArray();
        }

        public static int[] BuildCornerSectorIds(int radius)
        {
            var coords = EnumerateAxial(radius);
            var corners = GetCornerCoords(radius);
            var result = new int[corners.Count];

            for (var i = 0; i < corners.Count; i++)
            {
                result[i] = coords.IndexOf(corners[i]);
            }

            return result;
        }
    }
}
