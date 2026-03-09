namespace AIWarsIdle.PvPSim.Headless;

internal enum SimulationActorKind
{
    SimulatedPlayer,
    Bot
}

internal enum SimulationActorProfile
{
    Aggressive,
    Expansive,
    Defensive,
    Tactical
}

internal sealed class SimulationActorState
{
    public int PlayerId { get; init; }
    public SimulationActorKind Kind { get; init; }
    public SimulationActorProfile Profile { get; init; }
    public int RemainingAttacks { get; set; }
    public long LastRegenUnixSeconds { get; set; }
    public long NextRegenAtUnixSeconds { get; set; }
    public long LastDecisionBucket { get; set; } = long.MinValue;
}

internal sealed class SimulationRunResult
{
    public Dictionary<int, int> FinalOwnedCounts { get; init; } = new();
    public Dictionary<int, double> AverageShareByPlayer { get; init; } = new();
    public int WinnerPlayerId { get; init; }
    public bool HadDominantLeader { get; init; }
    public bool HadComeback { get; init; }
    public int AttackCount { get; init; }
    public int AttackWins { get; init; }
    public int AttacksWithUnderdog { get; init; }
    public int WinsWithUnderdog { get; init; }
    public int AttacksWithMaintenancePenalty { get; init; }
    public int WinsWithMaintenancePenalty { get; init; }
}

internal sealed class SimulationBatchReport
{
    public SimulationVariant Variant { get; init; }
    public int Runs { get; init; }
    public Dictionary<int, int> WinnerCounts { get; init; } = new();
    public Dictionary<int, double> AverageFinalOwned { get; init; } = new();
    public Dictionary<int, double> AverageShareByPlayer { get; init; } = new();
    public double DominantLeaderRate { get; init; }
    public double ComebackRate { get; init; }
    public double AttackWinRate { get; init; }
    public double UnderdogAttackWinRate { get; init; }
    public double MaintenancePenaltyWinRate { get; init; }
    public double UnderdogUsageRate { get; init; }
    public double MaintenanceUsageRate { get; init; }
}
