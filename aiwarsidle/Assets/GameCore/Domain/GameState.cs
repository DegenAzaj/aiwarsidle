using System;

namespace AIWarsIdle.GameCore.Domain
{
    public sealed class GameState
    {
        public const int GeneratorCount = 5;

        public double SoftCurrency;
        public double LifetimeEarnedSoftCurrency;
        public int PremiumCurrency;
        public int[] GeneratorLevels;
        public int PrestigeCount;
        public int PermanentUpgradeLevel;

        public int LeagueSeasonId;
        public int League;
        public int SeasonPoints;

        public int PvpAttacksRemaining;
        public long LastPvpAttackRegenUnixSeconds;
        public long LastPvpAdAttackClaimUnixSeconds;

        public MapState MapState;
        public OverclockState Overclock;

        public long LastLoginUnixSeconds;

        public GameState()
        {
            GeneratorLevels = new int[GeneratorCount];
            MapState = new MapState();
            Overclock = new OverclockState();
        }
    }
}
