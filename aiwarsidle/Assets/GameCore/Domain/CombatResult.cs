namespace AIWarsIdle.GameCore.Domain
{
    public sealed class CombatResult
    {
        public int SectorId;
        public bool Win;
        public BattleResult Battle = new();
        public SectorState UpdatedSector = new();
    }
}

