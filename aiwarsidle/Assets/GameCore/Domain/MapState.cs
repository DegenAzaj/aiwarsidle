using System;

namespace AIWarsIdle.GameCore.Domain
{
    public sealed class MapState
    {
        public int MapSeasonId;
        public long MatchStartUnixSeconds;
        public long CurrentUnixSeconds;
        public double MatchStartLocalPvpPower;
        public double MatchStartLocalBasePps;
        public double MatchStartLocalSoftCurrency;
        public double MatchStartLocalLifetimeEarnedSoftCurrency;
        public double MatchStartLocalLifetimeEarnedSoftCurrencyAtLastPrestige;
        public int MatchStartLocalPrestigeCount;
        public int MatchStartLocalPermanentUpgradeLevel;
        public int[] MatchStartLocalGeneratorLevels;
        public SectorState[] Sectors;

        public MapState()
        {
            MatchStartLocalGeneratorLevels = new int[GameState.GeneratorCount];
            Sectors = Array.Empty<SectorState>();
        }
    }
}
