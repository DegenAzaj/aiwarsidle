using System;
using AIWarsIdle.PvP.Config;
using UnityEngine;

namespace AIWarsIdle.PvP.Services
{
    public static class PvpRuntimeConfigFactory
    {
        public static MapConfig CreateRuntimeMapConfig(MapConfig source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));

            var runtime = UnityEngine.Object.Instantiate(source);
            runtime.name = $"{source.name}_Runtime";

            var needsGeneratedLayout =
                runtime.SectorDefinitions == null || runtime.SectorDefinitions.Length == 0;

            if (needsGeneratedLayout)
            {
                const int defaultRadius = 4;
                runtime.MapSeasonLengthDays = 3;
                runtime.EnforceSectorCountRange = true;
                runtime.MinSectorCount = HexGridTopology.ComputeCellCount(defaultRadius);
                runtime.MaxSectorCount = HexGridTopology.ComputeCellCount(defaultRadius);
                runtime.SectorDefinitions = HexGridTopology.BuildSectorDefinitions(defaultRadius);
                runtime.Adjacency = HexGridTopology.BuildAdjacency(defaultRadius);
                runtime.HomeSectorIds = HexGridTopology.BuildCornerSectorIds(defaultRadius);
                runtime.HomeSectorOwnerPlayerIds = new[] { 1, 2, 3, 4, 5, 6 };
                runtime.HomeSectorId = runtime.HomeSectorIds.Length > 0 ? runtime.HomeSectorIds[0] : 0;
                runtime.LocalPlayerId = 1;
                runtime.StabilityStartNeutral = Mathf.Clamp(runtime.StabilityStartNeutral, 0f, 100f);
                runtime.HomeSectorStability = Mathf.Clamp(runtime.HomeSectorStability, 0f, 100f);
            }
            else if ((runtime.HomeSectorIds == null || runtime.HomeSectorIds.Length == 0) && runtime.HomeSectorId >= 0)
            {
                runtime.HomeSectorIds = new[] { runtime.HomeSectorId };
                runtime.HomeSectorOwnerPlayerIds = new[] { runtime.LocalPlayerId };
            }

            runtime.ValidateOrThrow();
            return runtime;
        }
    }
}
