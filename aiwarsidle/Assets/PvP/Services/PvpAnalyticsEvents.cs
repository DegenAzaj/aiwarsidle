using AIWarsIdle.GameCore.Domain;

namespace AIWarsIdle.PvP.Services
{
    public readonly struct MapOpenedEvent
    {
        public readonly long NowUnixSeconds;

        public MapOpenedEvent(long nowUnixSeconds)
        {
            NowUnixSeconds = nowUnixSeconds;
        }
    }

    public readonly struct SectorViewedEvent
    {
        public readonly int SectorId;

        public SectorViewedEvent(int sectorId)
        {
            SectorId = sectorId;
        }
    }

    public readonly struct SectorAttackEvent
    {
        public readonly int SectorId;
        public readonly AttackStrategy Strategy;

        public SectorAttackEvent(int sectorId, AttackStrategy strategy)
        {
            SectorId = sectorId;
            Strategy = strategy;
        }
    }

    public readonly struct SectorResultEvent
    {
        public readonly int SectorId;
        public readonly bool Win;

        public SectorResultEvent(int sectorId, bool win)
        {
            SectorId = sectorId;
            Win = win;
        }
    }

    public readonly struct SectorOwnershipChangedEvent
    {
        public readonly int SectorId;
        public readonly int FromPlayerId;
        public readonly int ToPlayerId;

        public SectorOwnershipChangedEvent(int sectorId, int fromPlayerId, int toPlayerId)
        {
            SectorId = sectorId;
            FromPlayerId = fromPlayerId;
            ToPlayerId = toPlayerId;
        }
    }
}
