namespace AIWarsIdle.GameCore.Services
{
    public readonly struct OverclockActivatedEvent
    {
        public readonly long NowUnixSeconds;
        public readonly long ActiveUntilUnixSeconds;
        public readonly int ChargesRemaining;

        public OverclockActivatedEvent(long nowUnixSeconds, long activeUntilUnixSeconds, int chargesRemaining)
        {
            NowUnixSeconds = nowUnixSeconds;
            ActiveUntilUnixSeconds = activeUntilUnixSeconds;
            ChargesRemaining = chargesRemaining;
        }
    }

    public readonly struct OverclockChargeSpentEvent
    {
        public readonly long NowUnixSeconds;
        public readonly int ChargesRemaining;

        public OverclockChargeSpentEvent(long nowUnixSeconds, int chargesRemaining)
        {
            NowUnixSeconds = nowUnixSeconds;
            ChargesRemaining = chargesRemaining;
        }
    }

    public readonly struct OverclockChargeGainedEvent
    {
        public readonly long NowUnixSeconds;
        public readonly int Charges;

        public OverclockChargeGainedEvent(long nowUnixSeconds, int charges)
        {
            NowUnixSeconds = nowUnixSeconds;
            Charges = charges;
        }
    }
}

