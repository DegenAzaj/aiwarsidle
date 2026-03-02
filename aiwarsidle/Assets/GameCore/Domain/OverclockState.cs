namespace AIWarsIdle.GameCore.Domain
{
    public sealed class OverclockState
    {
        public int Charges;
        public long ActiveUntilUnixSeconds;
        public long NextChargeAtUnixSeconds;
    }
}

