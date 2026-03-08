using System;
using System.Collections.Generic;
using AIWarsIdle.GameCore.Domain;
using UnityEngine;

namespace AIWarsIdle.PvP.Config
{
    [CreateAssetMenu(menuName = "AI Wars Idle/Map Config", fileName = "MapConfig")]
    public sealed class MapConfig : ScriptableObject
    {
        [Serializable]
        public sealed class SectorDefinition
        {
            public int SectorId;
            public string Name;

            [Tooltip("Global production bonus contributed by owning this sector (in percent). Example: 5 = +5%.")]
            public float ProductionBonusPercent = 5f;

            [Tooltip("Flat PvP power bonus added to the owner's snapshot when this sector is owned.")]
            public double PvpPowerBonusFlat = 0.0;

            [Tooltip("Percent PvP power bonus (applied multiplicatively to core power) when this sector is owned. Example: 5 = +5%.")]
            public float PvpPowerBonusPercent = 0f;
        }

        [Serializable]
        public struct SectorEdge
        {
            public int A;
            public int B;
        }

        [Serializable]
        public sealed class DiminishingTier
        {
            [Min(1)]
            [Tooltip("Inclusive max owned sector count for this tier.")]
            public int MaxOwnedSectors = 3;

            [Range(0f, 1f)]
            [Tooltip("Multiplier applied to the sum of sector bonuses when owned sector count is within this tier.")]
            public float BonusMultiplier = 1f;
        }

        [Header("Season")]
        [Min(1)]
        public int MapSeasonLengthDays = 7;

        [Min(0)]
        [Tooltip("UnixTimeSeconds (UTC) anchor for season boundaries when using fixed-length seasons.")]
        public long MapSeasonAnchorUnixSecondsUtc = 0;

        [Header("Local player")]
        [Min(1)]
        [Tooltip("MVP: local player id used in core services.")]
        public int LocalPlayerId = 1;

        [Header("Home sector")]
        [Min(0)]
        public int HomeSectorId = 0;

        [Range(0f, 100f)]
        public float HomeSectorStability = 100f;

        [Tooltip("Optional protected home sectors for multi-faction maps. If empty, HomeSectorId is used.")]
        public int[] HomeSectorIds = Array.Empty<int>();

        [Tooltip("Optional owner ids aligned with HomeSectorIds. If empty, ids default to LocalPlayerId for the first home and increment for the rest.")]
        public int[] HomeSectorOwnerPlayerIds = Array.Empty<int>();

        [Header("Sectors")]
        public SectorDefinition[] SectorDefinitions = Array.Empty<SectorDefinition>();

        [Header("Validation (MVP guardrails)")]
        [Tooltip("When enabled, validates that SectorDefinitions count is within Min/Max range.")]
        public bool EnforceSectorCountRange = true;

        [Min(1)]
        public int MinSectorCount = 20;

        [Min(1)]
        public int MaxSectorCount = 30;

        [Tooltip("When enabled, validates that all sectors are reachable from HomeSectorId via Adjacency.")]
        public bool RequireConnectedGraph = true;

        [Header("Adjacency (no teleport)")]
        [Tooltip("Undirected edges. If (A,B) exists, A and B are neighbors.")]
        public SectorEdge[] Adjacency = Array.Empty<SectorEdge>();

        [Header("Stability")]
        [Min(0f)]
        public float StabilityGrowthPerSecond = 0.5f;

        [Min(0)]
        public int FreshCaptureWindowSeconds = 300;

        [Header("Capture / Combat")]
        [Range(0f, 100f)]
        public float StabilityStartNeutral = 0f;

        [Range(0f, 100f)]
        public float StabilityStartOnCapture = 10f;

        [Min(0f)]
        public float StabilityGainOnDefenseWin = 5f;

        [Min(0.01f)]
        public float CaptureStabilityMultiplierAggressive = 0.8f;

        [Min(0.01f)]
        public float CaptureStabilityMultiplierStable = 1.0f;

        [Min(0.01f)]
        public float CaptureStabilityMultiplierRisky = 0.6f;

        [Min(0)]
        [Tooltip("Optional. If >0, you cannot attack the same sector again until this cooldown expires.")]
        public int SectorAttackCooldownSeconds = 0;

        [Header("Matchmaking (MVP bots)")]
        [Min(0.01f)]
        public float BotPowerMinMultiplier = 0.8f;

        [Min(0.01f)]
        public float BotPowerMaxMultiplier = 1.2f;

        [Header("Neutral sectors")]
        [Min(0.01f)]
        public float NeutralPowerMinMultiplier = 0.45f;

        [Min(0.01f)]
        public float NeutralPowerMaxMultiplier = 0.65f;

        [Header("Rewards (MVP)")]
        [Min(0)]
        public double WinSoftReward = 50;

        [Min(0)]
        public double LoseSoftReward = 10;

        public int WinSeasonPoints = 10;
        public int LoseSeasonPoints = -2;

        [Header("Production bonus aggregation (MVP anti-snowball)")]
        [Min(0f)]
        [Tooltip("Optional cap on the (post-diminishing) sum of sector bonuses. 0 disables cap.")]
        public float SectorBonusCapPercent = 0f;

        [Tooltip("Tiered diminishing returns based on owned sector count. Leave empty to disable diminishing.")]
        public DiminishingTier[] ProductionBonusDiminishingTiers = new[]
        {
            new DiminishingTier { MaxOwnedSectors = 3, BonusMultiplier = 1.00f },
            new DiminishingTier { MaxOwnedSectors = 6, BonusMultiplier = 0.80f },
            new DiminishingTier { MaxOwnedSectors = 10, BonusMultiplier = 0.60f },
            new DiminishingTier { MaxOwnedSectors = 15, BonusMultiplier = 0.40f },
            new DiminishingTier { MaxOwnedSectors = 999, BonusMultiplier = 0.25f },
        };

        [Min(0)]
        public int MaintenanceFreeSectors = 5;

        [Min(0f)]
        [Tooltip("Each owned sector above MaintenanceFreeSectors reduces production by this percent. Example: 1 = -1% per sector.")]
        public float MaintenancePenaltyPercentPerExtraSector = 1f;

        [Range(0f, 1f)]
        [Tooltip("Minimum multiplier after maintenance penalty is applied.")]
        public float MaintenanceMinMultiplier = 0.1f;

        public void ValidateOrThrow()
        {
            if (MapSeasonLengthDays <= 0)
            {
                throw new InvalidOperationException($"{nameof(MapSeasonLengthDays)} must be > 0.");
            }
            if (MapSeasonAnchorUnixSecondsUtc < 0)
            {
                throw new InvalidOperationException($"{nameof(MapSeasonAnchorUnixSecondsUtc)} must be >= 0.");
            }
            if (LocalPlayerId <= 0)
            {
                throw new InvalidOperationException($"{nameof(LocalPlayerId)} must be > 0.");
            }

            ValidateHomeSectorsOrThrow();

            if (float.IsNaN(StabilityGrowthPerSecond) || float.IsInfinity(StabilityGrowthPerSecond) || StabilityGrowthPerSecond < 0f)
            {
                throw new InvalidOperationException($"{nameof(StabilityGrowthPerSecond)} must be finite and >= 0.");
            }
            if (FreshCaptureWindowSeconds < 0)
            {
                throw new InvalidOperationException($"{nameof(FreshCaptureWindowSeconds)} must be >= 0.");
            }

            ValidateStabilityPercentOrThrow(StabilityStartNeutral, nameof(StabilityStartNeutral));
            ValidateStabilityPercentOrThrow(HomeSectorStability, nameof(HomeSectorStability));
            ValidateStabilityPercentOrThrow(StabilityStartOnCapture, nameof(StabilityStartOnCapture));

            if (float.IsNaN(StabilityGainOnDefenseWin) || float.IsInfinity(StabilityGainOnDefenseWin) || StabilityGainOnDefenseWin < 0f)
            {
                throw new InvalidOperationException($"{nameof(StabilityGainOnDefenseWin)} must be finite and >= 0.");
            }

            ValidateFinitePositiveOrThrow(CaptureStabilityMultiplierAggressive, nameof(CaptureStabilityMultiplierAggressive));
            ValidateFinitePositiveOrThrow(CaptureStabilityMultiplierStable, nameof(CaptureStabilityMultiplierStable));
            ValidateFinitePositiveOrThrow(CaptureStabilityMultiplierRisky, nameof(CaptureStabilityMultiplierRisky));

            if (SectorAttackCooldownSeconds < 0)
            {
                throw new InvalidOperationException($"{nameof(SectorAttackCooldownSeconds)} must be >= 0.");
            }

            ValidateFinitePositiveOrThrow(BotPowerMinMultiplier, nameof(BotPowerMinMultiplier));
            ValidateFinitePositiveOrThrow(BotPowerMaxMultiplier, nameof(BotPowerMaxMultiplier));
            if (BotPowerMinMultiplier > BotPowerMaxMultiplier)
            {
                throw new InvalidOperationException($"{nameof(BotPowerMinMultiplier)} must be <= {nameof(BotPowerMaxMultiplier)}.");
            }

            ValidateFinitePositiveOrThrow(NeutralPowerMinMultiplier, nameof(NeutralPowerMinMultiplier));
            ValidateFinitePositiveOrThrow(NeutralPowerMaxMultiplier, nameof(NeutralPowerMaxMultiplier));
            if (NeutralPowerMinMultiplier > NeutralPowerMaxMultiplier)
            {
                throw new InvalidOperationException($"{nameof(NeutralPowerMinMultiplier)} must be <= {nameof(NeutralPowerMaxMultiplier)}.");
            }

            ValidateFiniteNonNegativeOrThrow(WinSoftReward, nameof(WinSoftReward));
            ValidateFiniteNonNegativeOrThrow(LoseSoftReward, nameof(LoseSoftReward));

            if (float.IsNaN(SectorBonusCapPercent) || float.IsInfinity(SectorBonusCapPercent) || SectorBonusCapPercent < 0f)
            {
                throw new InvalidOperationException($"{nameof(SectorBonusCapPercent)} must be finite and >= 0.");
            }
            if (MaintenanceFreeSectors < 0)
            {
                throw new InvalidOperationException($"{nameof(MaintenanceFreeSectors)} must be >= 0.");
            }
            if (float.IsNaN(MaintenancePenaltyPercentPerExtraSector) || float.IsInfinity(MaintenancePenaltyPercentPerExtraSector) || MaintenancePenaltyPercentPerExtraSector < 0f)
            {
                throw new InvalidOperationException($"{nameof(MaintenancePenaltyPercentPerExtraSector)} must be finite and >= 0.");
            }
            if (float.IsNaN(MaintenanceMinMultiplier) || float.IsInfinity(MaintenanceMinMultiplier) || MaintenanceMinMultiplier < 0f || MaintenanceMinMultiplier > 1f)
            {
                throw new InvalidOperationException($"{nameof(MaintenanceMinMultiplier)} must be finite and in range 0..1.");
            }

            ValidateSectorDefinitionsAndAdjacencyOrThrow();
            ValidateDiminishingTiersOrThrow();
        }

        public bool IsHomeSector(int sectorId)
        {
            var homeIds = GetEffectiveHomeSectorIds();
            for (var i = 0; i < homeIds.Length; i++)
            {
                if (homeIds[i] == sectorId) return true;
            }

            return false;
        }

        public int[] GetEffectiveHomeSectorIds()
        {
            if (HomeSectorIds != null && HomeSectorIds.Length > 0)
            {
                return HomeSectorIds;
            }

            return new[] { HomeSectorId };
        }

        public int GetHomeOwnerPlayerId(int sectorId)
        {
            var homeIds = GetEffectiveHomeSectorIds();
            for (var i = 0; i < homeIds.Length; i++)
            {
                if (homeIds[i] != sectorId) continue;
                return ResolveHomeOwnerPlayerIdByIndex(i);
            }

            return 0;
        }

        public int ResolveHomeOwnerPlayerIdByIndex(int index)
        {
            if (index < 0) return 0;

            if (HomeSectorOwnerPlayerIds != null &&
                index < HomeSectorOwnerPlayerIds.Length &&
                HomeSectorOwnerPlayerIds[index] > 0)
            {
                return HomeSectorOwnerPlayerIds[index];
            }

            return index == 0 ? LocalPlayerId : LocalPlayerId + index;
        }

        public float GetCaptureStabilityStart(AttackStrategy strategy)
        {
            var mult = strategy switch
            {
                AttackStrategy.Aggressive => CaptureStabilityMultiplierAggressive,
                AttackStrategy.Stable => CaptureStabilityMultiplierStable,
                AttackStrategy.Risky => CaptureStabilityMultiplierRisky,
                _ => 1f
            };

            var v = StabilityStartOnCapture * mult;
            if (v < 0f) v = 0f;
            if (v > 100f) v = 100f;
            return v;
        }

        private void ValidateSectorDefinitionsAndAdjacencyOrThrow()
        {
            if (SectorDefinitions == null || SectorDefinitions.Length == 0)
            {
                if (Adjacency != null && Adjacency.Length > 0)
                {
                    throw new InvalidOperationException($"{nameof(Adjacency)} requires non-empty {nameof(SectorDefinitions)}.");
                }
                return;
            }

            if (EnforceSectorCountRange)
            {
                if (MinSectorCount <= 0 || MaxSectorCount <= 0)
                {
                    throw new InvalidOperationException($"{nameof(MinSectorCount)} and {nameof(MaxSectorCount)} must be > 0.");
                }
                if (MinSectorCount > MaxSectorCount)
                {
                    throw new InvalidOperationException($"{nameof(MinSectorCount)} must be <= {nameof(MaxSectorCount)}.");
                }
                if (SectorDefinitions.Length < MinSectorCount || SectorDefinitions.Length > MaxSectorCount)
                {
                    throw new InvalidOperationException($"{nameof(SectorDefinitions)} must have {MinSectorCount}..{MaxSectorCount} entries for MVP.");
                }
            }

            var ids = new HashSet<int>();
            for (var i = 0; i < SectorDefinitions.Length; i++)
            {
                var def = SectorDefinitions[i];
                if (def == null) throw new InvalidOperationException($"{nameof(SectorDefinitions)}[{i}] must not be null.");

                if (def.SectorId < 0) throw new InvalidOperationException($"{nameof(SectorDefinitions)}[{i}].{nameof(SectorDefinition.SectorId)} must be >= 0.");
                if (!ids.Add(def.SectorId)) throw new InvalidOperationException($"Duplicate sector id: {def.SectorId}.");

                if (float.IsNaN(def.ProductionBonusPercent) || float.IsInfinity(def.ProductionBonusPercent))
                {
                    throw new InvalidOperationException($"{nameof(SectorDefinitions)}[{i}].{nameof(SectorDefinition.ProductionBonusPercent)} must be finite.");
                }

                if (def.ProductionBonusPercent < -100f || def.ProductionBonusPercent > 100f)
                {
                    throw new InvalidOperationException($"{nameof(SectorDefinitions)}[{i}].{nameof(SectorDefinition.ProductionBonusPercent)} must be in range -100..100.");
                }

                ValidateFiniteNonNegativeOrThrow(def.PvpPowerBonusFlat, $"{nameof(SectorDefinitions)}[{i}].{nameof(SectorDefinition.PvpPowerBonusFlat)}");

                if (float.IsNaN(def.PvpPowerBonusPercent) || float.IsInfinity(def.PvpPowerBonusPercent))
                {
                    throw new InvalidOperationException($"{nameof(SectorDefinitions)}[{i}].{nameof(SectorDefinition.PvpPowerBonusPercent)} must be finite.");
                }

                if (def.PvpPowerBonusPercent < -100f || def.PvpPowerBonusPercent > 100f)
                {
                    throw new InvalidOperationException($"{nameof(SectorDefinitions)}[{i}].{nameof(SectorDefinition.PvpPowerBonusPercent)} must be in range -100..100.");
                }
            }

            if (!ids.Contains(HomeSectorId))
            {
                throw new InvalidOperationException($"{nameof(HomeSectorId)} ({HomeSectorId}) must exist in {nameof(SectorDefinitions)}.");
            }

            if (Adjacency == null || Adjacency.Length == 0) return;

            var normalized = new HashSet<(int, int)>();
            for (var i = 0; i < Adjacency.Length; i++)
            {
                var e = Adjacency[i];
                if (e.A < 0 || e.B < 0) throw new InvalidOperationException($"{nameof(Adjacency)}[{i}] sector ids must be >= 0.");
                if (e.A == e.B) throw new InvalidOperationException($"{nameof(Adjacency)}[{i}] must not be a self-loop.");
                if (!ids.Contains(e.A) || !ids.Contains(e.B))
                {
                    throw new InvalidOperationException($"{nameof(Adjacency)}[{i}] references unknown sector id.");
                }

                var a = Math.Min(e.A, e.B);
                var b = Math.Max(e.A, e.B);
                if (!normalized.Add((a, b)))
                {
                    throw new InvalidOperationException($"{nameof(Adjacency)} contains duplicate edge ({a},{b}).");
                }
            }

            if (RequireConnectedGraph)
            {
                if (!ids.Contains(HomeSectorId))
                {
                    throw new InvalidOperationException($"{nameof(HomeSectorId)} ({HomeSectorId}) must exist in {nameof(SectorDefinitions)}.");
                }

                var visited = new HashSet<int>();
                var queue = new Queue<int>();
                visited.Add(HomeSectorId);
                queue.Enqueue(HomeSectorId);

                var neighbors = new Dictionary<int, List<int>>();
                foreach (var id in ids)
                {
                    neighbors[id] = new List<int>();
                }
                foreach (var e in normalized)
                {
                    neighbors[e.Item1].Add(e.Item2);
                    neighbors[e.Item2].Add(e.Item1);
                }

                while (queue.Count > 0)
                {
                    var cur = queue.Dequeue();
                    var list = neighbors[cur];
                    for (var i = 0; i < list.Count; i++)
                    {
                        var n = list[i];
                        if (!visited.Add(n)) continue;
                        queue.Enqueue(n);
                    }
                }

                if (visited.Count != ids.Count)
                {
                    throw new InvalidOperationException($"{nameof(Adjacency)} graph must be connected (reachable from {nameof(HomeSectorId)}).");
                }
            }
        }

        private void ValidateHomeSectorsOrThrow()
        {
            var homeIds = GetEffectiveHomeSectorIds();
            if (homeIds.Length == 0)
            {
                throw new InvalidOperationException("At least one home sector is required.");
            }

            var seen = new HashSet<int>();
            for (var i = 0; i < homeIds.Length; i++)
            {
                if (homeIds[i] < 0)
                {
                    throw new InvalidOperationException($"{nameof(HomeSectorIds)}[{i}] must be >= 0.");
                }

                if (!seen.Add(homeIds[i]))
                {
                    throw new InvalidOperationException($"{nameof(HomeSectorIds)} must not contain duplicates.");
                }
            }

            if (HomeSectorOwnerPlayerIds != null &&
                HomeSectorOwnerPlayerIds.Length > 0 &&
                HomeSectorOwnerPlayerIds.Length != homeIds.Length)
            {
                throw new InvalidOperationException($"{nameof(HomeSectorOwnerPlayerIds)} length must match {nameof(HomeSectorIds)} length when provided.");
            }

            if (HomeSectorOwnerPlayerIds == null) return;

            for (var i = 0; i < HomeSectorOwnerPlayerIds.Length; i++)
            {
                if (HomeSectorOwnerPlayerIds[i] <= 0)
                {
                    throw new InvalidOperationException($"{nameof(HomeSectorOwnerPlayerIds)}[{i}] must be > 0.");
                }
            }
        }

        private void ValidateDiminishingTiersOrThrow()
        {
            if (ProductionBonusDiminishingTiers == null || ProductionBonusDiminishingTiers.Length == 0) return;

            var lastMax = 0;
            for (var i = 0; i < ProductionBonusDiminishingTiers.Length; i++)
            {
                var tier = ProductionBonusDiminishingTiers[i];
                if (tier == null) throw new InvalidOperationException($"{nameof(ProductionBonusDiminishingTiers)}[{i}] must not be null.");
                if (tier.MaxOwnedSectors <= 0) throw new InvalidOperationException($"{nameof(ProductionBonusDiminishingTiers)}[{i}].{nameof(DiminishingTier.MaxOwnedSectors)} must be > 0.");
                if (tier.MaxOwnedSectors < lastMax) throw new InvalidOperationException($"{nameof(ProductionBonusDiminishingTiers)} must be ascending by {nameof(DiminishingTier.MaxOwnedSectors)}.");
                lastMax = tier.MaxOwnedSectors;

                if (float.IsNaN(tier.BonusMultiplier) || float.IsInfinity(tier.BonusMultiplier) || tier.BonusMultiplier < 0f || tier.BonusMultiplier > 1f)
                {
                    throw new InvalidOperationException($"{nameof(ProductionBonusDiminishingTiers)}[{i}].{nameof(DiminishingTier.BonusMultiplier)} must be finite and in range 0..1.");
                }
            }
        }

        private static void ValidateStabilityPercentOrThrow(float value, string name)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value < 0f || value > 100f)
            {
                throw new InvalidOperationException($"{name} must be finite and in range 0..100.");
            }
        }

        private static void ValidateFinitePositiveOrThrow(float value, string name)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                throw new InvalidOperationException($"{name} must be finite.");
            }
            if (value <= 0f)
            {
                throw new InvalidOperationException($"{name} must be > 0.");
            }
        }

        private static void ValidateFiniteNonNegativeOrThrow(double value, string name)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                throw new InvalidOperationException($"{name} must be finite.");
            }
            if (value < 0.0)
            {
                throw new InvalidOperationException($"{name} must be >= 0.");
            }
        }
    }
}
