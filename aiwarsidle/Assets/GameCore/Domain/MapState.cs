using System;

namespace AIWarsIdle.GameCore.Domain
{
    public sealed class MapState
    {
        public int MapSeasonId;
        public SectorState[] Sectors;

        public MapState()
        {
            Sectors = Array.Empty<SectorState>();
        }
    }
}

