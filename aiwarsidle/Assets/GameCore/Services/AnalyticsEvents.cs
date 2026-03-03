namespace AIWarsIdle.GameCore.Services
{
    public readonly struct OfflineClaimedEvent
    {
        public readonly double BaseAmount;
        public readonly double Multiplier;
        public readonly double FinalAmount;

        public OfflineClaimedEvent(double baseAmount, double multiplier)
        {
            BaseAmount = baseAmount;
            Multiplier = multiplier;
            FinalAmount = baseAmount * multiplier;
        }
    }

    public readonly struct GeneratorUpgradedEvent
    {
        public readonly int GeneratorId;
        public readonly int NewLevel;
        public readonly int UpgradeCount;

        public GeneratorUpgradedEvent(int generatorId, int newLevel, int upgradeCount)
        {
            GeneratorId = generatorId;
            NewLevel = newLevel;
            UpgradeCount = upgradeCount;
        }
    }

    public readonly struct PrestigeExecutedEvent
    {
        public readonly int NewPrestigeCount;
        public readonly int NewPermanentUpgradeLevel;

        public PrestigeExecutedEvent(int newPrestigeCount, int newPermanentUpgradeLevel)
        {
            NewPrestigeCount = newPrestigeCount;
            NewPermanentUpgradeLevel = newPermanentUpgradeLevel;
        }
    }

    public readonly struct AdWatchedEvent
    {
        public readonly string Placement;

        public AdWatchedEvent(string placement)
        {
            Placement = placement;
        }
    }

    public readonly struct SubscriptionStartedEvent
    {
        public readonly string Source;

        public SubscriptionStartedEvent(string source)
        {
            Source = source;
        }
    }

    public readonly struct SessionStartedEvent
    {
        public readonly long NowUnixSeconds;

        public SessionStartedEvent(long nowUnixSeconds)
        {
            NowUnixSeconds = nowUnixSeconds;
        }
    }
}
