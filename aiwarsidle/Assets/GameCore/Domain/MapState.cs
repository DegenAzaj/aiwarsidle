using System;

namespace AIWarsIdle.GameCore.Domain
{
    public sealed class MapState
    {
        public int MapSeasonId;
        public long MatchStartUnixSeconds;
        public long CurrentUnixSeconds;
        public long BotAttackStatesMatchStartUnixSeconds;
        public double MatchStartLocalPvpPower;
        public double MatchStartLocalBasePps;
        public double MatchStartLocalSoftCurrency;
        public double MatchStartLocalLifetimeEarnedSoftCurrency;
        public double MatchStartLocalLifetimeEarnedSoftCurrencyAtLastPrestige;
        public int MatchStartLocalPrestigeCount;
        public int MatchStartLocalPermanentUpgradeLevel;
        public int[] MatchStartLocalGeneratorLevels;
        public BotAttackState[] BotAttackStates;
        public SectorState[] Sectors;

        public MapState()
        {
            MatchStartLocalGeneratorLevels = new int[GameState.GeneratorCount];
            BotAttackStates = Array.Empty<BotAttackState>();
            Sectors = Array.Empty<SectorState>();
        }
    }

    public sealed class BotAttackState
    {
        public int PlayerId;
        public int Remaining;
        public long LastRegenUnixSeconds;
        public long NextRegenAtUnixSeconds;
        public bool HasLastDecisionBucket;
        public long LastDecisionBucket;
    }
}
