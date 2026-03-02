namespace AIWarsIdle.GameCore.Domain
{
    public sealed class SectorState
    {
        public int SectorId;
        public int OwnerPlayerId; // MVP: 0 = neutral/bot/unknown
        public PvpSnapshot OwnerSnapshot = new();
        public float Stability; // 0..100
        public long LastCombatUnixSeconds;
        public long CapturedUnixSeconds;
    }
}

