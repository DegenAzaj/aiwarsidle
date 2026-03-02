namespace AIWarsIdle.GameCore.Services
{
    public readonly struct CurrencyChangedEvent
    {
        public readonly double NewBalance;
        public readonly double NewLifetimeEarned;
        public readonly double Delta;
        public readonly CurrencySource Source;

        public CurrencyChangedEvent(double newBalance, double newLifetimeEarned, double delta, CurrencySource source)
        {
            NewBalance = newBalance;
            NewLifetimeEarned = newLifetimeEarned;
            Delta = delta;
            Source = source;
        }
    }
}

