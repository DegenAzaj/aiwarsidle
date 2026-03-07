namespace AIWarsIdle.PvP.Services
{
    public enum PvpAttackBlockReason
    {
        None = 0,
        MapUninitialized = 1,
        SeasonResetPending = 2,
        SectorMissing = 3,
        HomeSector = 4,
        AlreadyOwned = 5,
        MissingMapDefinitions = 6,
        NoAdjacentOwnedSector = 7,
        CooldownActive = 8,
        NoAttackCharges = 9
    }

    public struct PvpAttackEvaluation
    {
        public bool CanAttack;
        public PvpAttackBlockReason BlockReason;
        public int RemainingAttacks;
        public long CooldownEndsAtUnixSeconds;
        public long CooldownRemainingSeconds;
    }
}
