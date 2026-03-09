namespace AIWarsIdle.PvPSim.Headless;

internal static class SimulationReportPrinter
{
    public static void Print(IReadOnlyList<SimulationBatchReport> reports, SimulationOptions options)
    {
        Console.WriteLine("AIWarsIdle PvP headless simulation");
        Console.WriteLine($"runs={options.Runs} duration_minutes={options.DurationMinutes} step_seconds={options.StepSeconds} radius={options.Radius} factions={options.Factions} seed={options.Seed}");
        Console.WriteLine();

        for (var i = 0; i < reports.Count; i++)
        {
            var report = reports[i];
            Console.WriteLine($"[{report.Variant}]");
            Console.WriteLine($"dominant_leader_rate={report.DominantLeaderRate:P1} comeback_rate={report.ComebackRate:P1} attack_win_rate={report.AttackWinRate:P1}");
            Console.WriteLine($"underdog_usage_rate={report.UnderdogUsageRate:P1} underdog_attack_win_rate={report.UnderdogAttackWinRate:P1}");
            Console.WriteLine($"maintenance_usage_rate={report.MaintenanceUsageRate:P1} maintenance_attack_win_rate={report.MaintenancePenaltyWinRate:P1}");

            var playerIds = report.AverageShareByPlayer.Keys
                .Concat(report.AverageFinalOwned.Keys)
                .Concat(report.WinnerCounts.Keys)
                .Distinct()
                .OrderBy(id => id);

            foreach (var playerId in playerIds)
            {
                report.WinnerCounts.TryGetValue(playerId, out var wins);
                var winnerRate = report.Runs <= 0 ? 0 : wins / (double)report.Runs;
                report.AverageFinalOwned.TryGetValue(playerId, out var avgOwned);
                report.AverageShareByPlayer.TryGetValue(playerId, out var avgShare);
                Console.WriteLine($"player_{playerId}: win_rate={winnerRate:P1} avg_final_owned={avgOwned:0.00} avg_share={avgShare:P1}");
            }

            if (i + 1 < reports.Count)
            {
                Console.WriteLine();
            }
        }
    }
}
