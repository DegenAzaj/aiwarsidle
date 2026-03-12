using System.Runtime.Serialization;
using AIWarsIdle.GameCore.Config;
using AIWarsIdle.GameCore.Domain;
using AIWarsIdle.PvP.Config;

namespace AIWarsIdle.PvPSim.Headless;

internal static class SimulationFactories
{
    public static GameState CreateInitialState(MapConfig mapConfig)
    {
        var state = new GameState
        {
            PrestigeCount = 5,
            PermanentUpgradeLevel = 0,
            PvpAttacksRemaining = 0
        };

        state.GeneratorLevels[0] = 8;
        state.GeneratorLevels[1] = 4;
        state.GeneratorLevels[2] = 2;
        state.MapState.Sectors = CreateInitialSectors(mapConfig);
        return state;
    }

    public static BalanceConfig CreateBalanceConfig()
    {
        var cfg = CreateUninitialized<BalanceConfig>();
        cfg.GeneratorBaseCosts = new[] { 10d, 100d, 1000d, 10000d, 100000d };
        cfg.GeneratorCostGrowthFactors = new[] { 1.15d, 1.15d, 1.15d, 1.15d, 1.15d };
        cfg.GeneratorBaseOutputs = new[] { 1.0d, 2.5d, 5.0d, 10.0d, 25.0d };
        cfg.MilestoneEveryLevels = 25;
        cfg.MilestoneMultiplier = 2.0d;
        cfg.PrestigeThresholdBase = 1000d;
        cfg.PrestigeThresholdGrowthFactor = 1.6d;
        cfg.PermanentUpgradeCap = 10;
        cfg.PrestigeMultiplierIncrease = 0.05d;
        cfg.OfflineCapSeconds = 12 * 60 * 60;
        cfg.OfflineEfficiency = 0.6d;
        cfg.ValidateOrThrow();
        return cfg;
    }

    public static PvpConfig CreatePvpConfig(SimulationVariant variant)
    {
        var cfg = CreateUninitialized<PvpConfig>();
        cfg.PowerVarianceMin = 0.95f;
        cfg.PowerVarianceMax = 1.05f;
        cfg.StrategyMultiplierAggressive = 1.08f;
        cfg.StrategyMultiplierStable = 0.98f;
        cfg.StrategyMultiplierRisky = 1.16f;
        cfg.StabilityMultiplierMin = 0.80f;
        cfg.StabilityMultiplierMax = 1.35f;
        cfg.DefenseBonus = 1.10f;
        cfg.FlankBonusPerExtraAttacker = 0.10f;
        cfg.FlankBonusMaxMultiplier = 1.30f;
        cfg.CombatMaintenanceFreeSectors = variant == SimulationVariant.NoMaintenance || variant == SimulationVariant.NoUnderdogNoMaintenance ? 999 : 6;
        cfg.CombatMaintenancePenaltyPerExtraSector = variant == SimulationVariant.NoMaintenance || variant == SimulationVariant.NoUnderdogNoMaintenance ? 0f : 0.03f;
        cfg.CombatMaintenanceMinMultiplier = variant == SimulationVariant.NoMaintenance || variant == SimulationVariant.NoUnderdogNoMaintenance ? 1f : 0.75f;
        cfg.UnderdogMaxAttackBonus = variant == SimulationVariant.NoUnderdog || variant == SimulationVariant.NoUnderdogNoMaintenance ? 0f : 0.25f;
        cfg.UnderdogSectorDeficitForMaxBonus = 8;
        cfg.PrestigePvpPowerPerPrestige = 12.0;
        cfg.PermanentPvpPowerPerLevel = 2.0;
        cfg.SectorPvpPowerPerSector = 1.5;
        cfg.ProdToPvpMaxBonus = 0.20;
        cfg.ProdToPvpHalfCapPps = 100.0;
        cfg.ValidateOrThrow();
        return cfg;
    }

    public static PvpAttacksConfig CreateAttacksConfig()
    {
        var cfg = CreateUninitialized<PvpAttacksConfig>();
        cfg.MaxAttacks = 5;
        cfg.RegenSeconds = 2 * 60 * 60;
        cfg.AdExtraAttacksPerDay = 0;
        cfg.PremiumExtraAttacksPerPurchase = 0;
        cfg.PremiumCurrencyCostPerPurchase = 0;
        cfg.ValidateOrThrow();
        return cfg;
    }

    public static MapConfig CreateMapConfig(int radius, int factions)
    {
        var cfg = CreateUninitialized<MapConfig>();
        var sectorDefinitions = HeadlessHexTopology.BuildSectorDefinitions(radius);
        var adjacency = HeadlessHexTopology.BuildAdjacency(radius);
        var homeIds = HeadlessHexTopology.BuildEvenlySpacedCornerSectorIds(radius, factions);
        var ownerIds = Enumerable.Range(1, factions).ToArray();

        cfg.MapSeasonLengthDays = 7;
        cfg.MapSeasonAnchorUnixSecondsUtc = 0;
        cfg.LocalPlayerId = 1;
        cfg.HomeSectorId = homeIds[0];
        cfg.HomeSectorStability = 100f;
        cfg.HomeSectorIds = homeIds;
        cfg.HomeSectorOwnerPlayerIds = ownerIds;
        cfg.SectorDefinitions = sectorDefinitions;
        cfg.EnforceSectorCountRange = false;
        cfg.MinSectorCount = sectorDefinitions.Length;
        cfg.MaxSectorCount = sectorDefinitions.Length;
        cfg.RequireConnectedGraph = true;
        cfg.Adjacency = adjacency;
        cfg.StabilityGrowthPerSecond = 0.03f;
        cfg.FreshCaptureWindowSeconds = 300;
        cfg.StabilityStartNeutral = 0f;
        cfg.StabilityStartOnCapture = 10f;
        cfg.StabilityGainOnDefenseWin = 5f;
        cfg.CaptureStabilityMultiplierAggressive = 0.7f;
        cfg.CaptureStabilityMultiplierStable = 1.6f;
        cfg.CaptureStabilityMultiplierRisky = 0.3f;
        cfg.SectorAttackCooldownSeconds = 0;
        cfg.BotPowerMinMultiplier = 0.90f;
        cfg.BotPowerMaxMultiplier = 1.10f;
        cfg.NeutralPowerMinMultiplier = 0.45f;
        cfg.NeutralPowerMaxMultiplier = 0.65f;
        cfg.WinSoftReward = 0;
        cfg.LoseSoftReward = 0;
        cfg.WinSeasonPoints = 0;
        cfg.LoseSeasonPoints = 0;
        cfg.SectorBonusCapPercent = 0f;
        cfg.ProductionBonusDiminishingTiers = new[]
        {
            new MapConfig.DiminishingTier { MaxOwnedSectors = 3, BonusMultiplier = 1.0f },
            new MapConfig.DiminishingTier { MaxOwnedSectors = 6, BonusMultiplier = 0.8f },
            new MapConfig.DiminishingTier { MaxOwnedSectors = 10, BonusMultiplier = 0.6f },
            new MapConfig.DiminishingTier { MaxOwnedSectors = 999, BonusMultiplier = 0.25f }
        };
        cfg.MaintenanceFreeSectors = 5;
        cfg.MaintenancePenaltyPercentPerExtraSector = 1f;
        cfg.MaintenanceMinMultiplier = 0.1f;
        cfg.ValidateOrThrow();
        return cfg;
    }

    private static SectorState[] CreateInitialSectors(MapConfig mapConfig)
    {
        var sectors = new SectorState[mapConfig.SectorDefinitions.Length];
        for (var i = 0; i < mapConfig.SectorDefinitions.Length; i++)
        {
            var def = mapConfig.SectorDefinitions[i];
            var isHome = mapConfig.IsHomeSector(def.SectorId);

            sectors[i] = new SectorState
            {
                SectorId = def.SectorId,
                OwnerPlayerId = isHome ? mapConfig.GetHomeOwnerPlayerId(def.SectorId) : 0,
                OwnerSnapshot = new PvpSnapshot(),
                Stability = isHome ? mapConfig.HomeSectorStability : mapConfig.StabilityStartNeutral,
                LastCombatUnixSeconds = 0,
                CapturedUnixSeconds = isHome ? 1 : 0
            };
        }

        return sectors;
    }

    private static T CreateUninitialized<T>() where T : class
    {
#pragma warning disable SYSLIB0050
        return (T)FormatterServices.GetUninitializedObject(typeof(T));
#pragma warning restore SYSLIB0050
    }
}

internal static class HeadlessHexTopology
{
    public static MapConfig.SectorDefinition[] BuildSectorDefinitions(int radius)
    {
        var coords = EnumerateAxial(radius);
        var defs = new MapConfig.SectorDefinition[coords.Count];

        for (var i = 0; i < coords.Count; i++)
        {
            defs[i] = new MapConfig.SectorDefinition
            {
                SectorId = i,
                Name = $"Hex {coords[i].Q},{coords[i].R}",
                ProductionBonusPercent = 5f,
                PvpPowerBonusFlat = 0.0,
                PvpPowerBonusPercent = 0f
            };
        }

        return defs;
    }

    public static MapConfig.SectorEdge[] BuildAdjacency(int radius)
    {
        var coords = EnumerateAxial(radius);
        var byCoord = coords
            .Select((coord, index) => new { coord, index })
            .ToDictionary(x => x.coord, x => x.index);

        var edges = new List<MapConfig.SectorEdge>();
        var directions = new[]
        {
            (1, 0),
            (1, -1),
            (0, -1),
            (-1, 0),
            (-1, 1),
            (0, 1)
        };

        for (var i = 0; i < coords.Count; i++)
        {
            var coord = coords[i];
            foreach (var (dq, dr) in directions)
            {
                var neighbor = (coord.Q + dq, coord.R + dr);
                if (!byCoord.TryGetValue(neighbor, out var neighborId)) continue;
                if (neighborId <= i) continue;
                edges.Add(new MapConfig.SectorEdge { A = i, B = neighborId });
            }
        }

        return edges.ToArray();
    }

    public static int[] BuildEvenlySpacedCornerSectorIds(int radius, int factions)
    {
        var coords = EnumerateAxial(radius);
        var corners = new List<(int Q, int R)>
        {
            (radius, 0),
            (0, radius),
            (-radius, radius),
            (-radius, 0),
            (0, -radius),
            (radius, -radius)
        };

        var cornerIds = corners.Select(corner => coords.IndexOf(corner)).Where(id => id >= 0).ToArray();
        var selected = new List<int>(factions);
        for (var i = 0; i < factions; i++)
        {
            var idx = (int)Math.Round(i * (cornerIds.Length / (double)factions)) % cornerIds.Length;
            var candidate = cornerIds[idx];
            while (selected.Contains(candidate))
            {
                idx = (idx + 1) % cornerIds.Length;
                candidate = cornerIds[idx];
            }
            selected.Add(candidate);
        }

        return selected.ToArray();
    }

    private static List<(int Q, int R)> EnumerateAxial(int radius)
    {
        if (radius < 0) radius = 0;
        var coords = new List<(int Q, int R)>(1 + (3 * radius * (radius + 1)));

        for (var q = -radius; q <= radius; q++)
        {
            var rMin = Math.Max(-radius, -q - radius);
            var rMax = Math.Min(radius, -q + radius);

            for (var r = rMin; r <= rMax; r++)
            {
                coords.Add((q, r));
            }
        }

        coords.Sort((a, b) =>
        {
            var byR = b.R.CompareTo(a.R);
            return byR != 0 ? byR : a.Q.CompareTo(b.Q);
        });

        return coords;
    }
}
