using AIWarsIdle.GameCore.Config;
using AIWarsIdle.GameCore.Domain;
using AIWarsIdle.GameCore.Services;
using AIWarsIdle.PvP.Config;
using AIWarsIdle.PvP.Services;

namespace AIWarsIdle.PvPSim.Headless;

internal sealed class SimulationBatchRunner
{
    public SimulationBatchReport Run(SimulationOptions options)
    {
        return RunVariant(options, options.Variant);
    }

    public IReadOnlyList<SimulationBatchReport> RunComparison(SimulationOptions options)
    {
        return new[]
        {
            RunVariant(options, SimulationVariant.Baseline),
            RunVariant(options, SimulationVariant.NoUnderdog),
            RunVariant(options, SimulationVariant.NoMaintenance),
            RunVariant(options, SimulationVariant.NoUnderdogNoMaintenance)
        };
    }

    private SimulationBatchReport RunVariant(SimulationOptions options, SimulationVariant variant)
    {
        var winnerCounts = new Dictionary<int, int>();
        var totalFinalOwned = new Dictionary<int, double>();
        var totalAverageShares = new Dictionary<int, double>();
        var dominantRuns = 0;
        var comebackRuns = 0;
        var totalAttacks = 0;
        var totalAttackWins = 0;
        var totalUnderdogAttacks = 0;
        var totalUnderdogWins = 0;
        var totalMaintenanceAttacks = 0;
        var totalMaintenanceWins = 0;

        for (var run = 0; run < options.Runs; run++)
        {
            var seed = unchecked(options.Seed + (run * 7919));
            var result = RunSingle(options, variant, seed);

            Increment(winnerCounts, result.WinnerPlayerId, 1);
            AddOwned(totalFinalOwned, result.FinalOwnedCounts);
            AddShares(totalAverageShares, result.AverageShareByPlayer);

            if (result.HadDominantLeader) dominantRuns++;
            if (result.HadComeback) comebackRuns++;

            totalAttacks += result.AttackCount;
            totalAttackWins += result.AttackWins;
            totalUnderdogAttacks += result.AttacksWithUnderdog;
            totalUnderdogWins += result.WinsWithUnderdog;
            totalMaintenanceAttacks += result.AttacksWithMaintenancePenalty;
            totalMaintenanceWins += result.WinsWithMaintenancePenalty;
        }

        Normalize(totalFinalOwned, options.Runs);
        Normalize(totalAverageShares, options.Runs);

        return new SimulationBatchReport
        {
            Variant = variant,
            Runs = options.Runs,
            WinnerCounts = winnerCounts,
            AverageFinalOwned = totalFinalOwned,
            AverageShareByPlayer = totalAverageShares,
            DominantLeaderRate = options.Runs <= 0 ? 0 : dominantRuns / (double)options.Runs,
            ComebackRate = options.Runs <= 0 ? 0 : comebackRuns / (double)options.Runs,
            AttackWinRate = Rate(totalAttackWins, totalAttacks),
            UnderdogAttackWinRate = Rate(totalUnderdogWins, totalUnderdogAttacks),
            MaintenancePenaltyWinRate = Rate(totalMaintenanceWins, totalMaintenanceAttacks),
            UnderdogUsageRate = Rate(totalUnderdogAttacks, totalAttacks),
            MaintenanceUsageRate = Rate(totalMaintenanceAttacks, totalAttacks)
        };
    }

    private SimulationRunResult RunSingle(SimulationOptions options, SimulationVariant variant, int seed)
    {
        var mapConfig = SimulationFactories.CreateMapConfig(options.Radius, options.Factions);
        var balanceConfig = SimulationFactories.CreateBalanceConfig();
        var pvpConfig = SimulationFactories.CreatePvpConfig(variant);
        var attacksConfig = SimulationFactories.CreateAttacksConfig();
        var state = SimulationFactories.CreateInitialState(mapConfig);

        var economy = new EconomyService(state);
        var production = new ProductionService(state, balanceConfig, economy);
        var snapshot = new SnapshotService(state, pvpConfig, production, mapConfig.LocalPlayerId, mapConfig);
        var factions = new FactionSnapshotService(state, mapConfig, pvpConfig, snapshot);
        var matchmaking = new MatchmakingService(mapConfig, factions);
        var battleSim = new BattleSimService(pvpConfig);
        var map = new MapService(state.MapState, mapConfig);

        const long startNow = 1000;
        var totalSteps = Math.Max(1, (options.DurationMinutes * 60) / options.StepSeconds);
        var factionIds = mapConfig.GetEffectiveHomeSectorIds()
            .Select(mapConfig.GetHomeOwnerPlayerId)
            .Where(id => id > 0)
            .Distinct()
            .ToArray();

        var actorStates = BuildActorStates(factionIds, attacksConfig);
        var totalSectors = mapConfig.SectorDefinitions.Length;
        var cumulativeShare = factionIds.ToDictionary(id => id, _ => 0d);
        var halfStepLeader = 0;
        var hadDominantLeader = false;
        var attackCount = 0;
        var attackWins = 0;
        var underdogAttacks = 0;
        var underdogWins = 0;
        var maintenanceAttacks = 0;
        var maintenanceWins = 0;

        map.AdvanceTime(startNow);

        for (var step = 1; step <= totalSteps; step++)
        {
            var now = startNow + (step * options.StepSeconds);
            map.AdvanceTime(now);

            var order = BuildProcessingOrder(factionIds, seed, step);
            for (var i = 0; i < order.Count; i++)
            {
                var actor = actorStates[order[i]];
                TickCharges(actor, attacksConfig, now);
                var interval = GetDecisionIntervalSeconds(actor);
                var bucket = now / interval;
                if (actor.LastDecisionBucket == bucket) continue;
                if (actor.RemainingAttacks <= 0) continue;

                actor.LastDecisionBucket = bucket;
                var attack = ChooseAttack(actor, now, seed, map, mapConfig, state, factions, matchmaking, battleSim);
                if (attack == null) continue;

                actor.RemainingAttacks--;
                if (actor.RemainingAttacks < attacksConfig.MaxAttacks &&
                    (actor.NextRegenAtUnixSeconds <= 0 || actor.NextRegenAtUnixSeconds <= now))
                {
                    actor.LastRegenUnixSeconds = now;
                    actor.NextRegenAtUnixSeconds = now + attacksConfig.RegenSeconds;
                }

                var result = ResolveAttack(
                    attack.Value,
                    now,
                    seed,
                    mapConfig,
                    state,
                    map,
                    factions,
                    matchmaking,
                    battleSim);

                attackCount++;
                if (result.Win) attackWins++;
                if (result.HadUnderdogBonus)
                {
                    underdogAttacks++;
                    if (result.Win) underdogWins++;
                }
                if (result.HadMaintenancePenalty)
                {
                    maintenanceAttacks++;
                    if (result.Win) maintenanceWins++;
                }
            }

            var connectedCounts = factionIds.ToDictionary(id => id, id => MapConnectivityService.BuildHomeConnectedSectorSet(state.MapState, mapConfig, id).Count);
            foreach (var kvp in connectedCounts)
            {
                cumulativeShare[kvp.Key] += totalSectors <= 0 ? 0 : kvp.Value / (double)totalSectors;
            }

            var leader = connectedCounts.OrderByDescending(kvp => kvp.Value).ThenBy(kvp => kvp.Key).First().Key;
            if (step == Math.Max(1, totalSteps / 2))
            {
                halfStepLeader = leader;
            }

            var dominant = connectedCounts.Values.Any(count => totalSectors > 0 && count / (double)totalSectors >= 0.60);
            if (dominant) hadDominantLeader = true;
        }

        var finalOwnedCounts = factionIds.ToDictionary(id => id, id => MapConnectivityService.BuildHomeConnectedSectorSet(state.MapState, mapConfig, id).Count);
        var winner = finalOwnedCounts.OrderByDescending(kvp => kvp.Value).ThenBy(kvp => kvp.Key).First().Key;
        var averageShare = cumulativeShare.ToDictionary(kvp => kvp.Key, kvp => kvp.Value / totalSteps);

        return new SimulationRunResult
        {
            FinalOwnedCounts = finalOwnedCounts,
            AverageShareByPlayer = averageShare,
            WinnerPlayerId = winner,
            HadDominantLeader = hadDominantLeader,
            HadComeback = halfStepLeader > 0 && halfStepLeader != winner,
            AttackCount = attackCount,
            AttackWins = attackWins,
            AttacksWithUnderdog = underdogAttacks,
            WinsWithUnderdog = underdogWins,
            AttacksWithMaintenancePenalty = maintenanceAttacks,
            WinsWithMaintenancePenalty = maintenanceWins
        };
    }

    private static Dictionary<int, SimulationActorState> BuildActorStates(int[] factionIds, PvpAttacksConfig attacksConfig)
    {
        var states = new Dictionary<int, SimulationActorState>(factionIds.Length);
        for (var i = 0; i < factionIds.Length; i++)
        {
            var playerId = factionIds[i];
            states[playerId] = new SimulationActorState
            {
                PlayerId = playerId,
                Kind = playerId == 1 ? SimulationActorKind.SimulatedPlayer : SimulationActorKind.Bot,
                Profile = ResolveProfile(playerId),
                RemainingAttacks = attacksConfig.MaxAttacks,
                LastRegenUnixSeconds = 0,
                NextRegenAtUnixSeconds = 0
            };
        }

        return states;
    }

    private static List<int> BuildProcessingOrder(int[] factionIds, int seed, int step)
    {
        var values = factionIds.ToList();
        var rng = new Random(unchecked((seed * 31) + step));

        for (var i = values.Count - 1; i > 0; i--)
        {
            var j = rng.Next(i + 1);
            (values[i], values[j]) = (values[j], values[i]);
        }

        return values;
    }

    private static void TickCharges(SimulationActorState actor, PvpAttacksConfig attacksConfig, long nowUnixSeconds)
    {
        if (actor.RemainingAttacks >= attacksConfig.MaxAttacks)
        {
            if (actor.NextRegenAtUnixSeconds > 0 && actor.NextRegenAtUnixSeconds <= nowUnixSeconds)
            {
                actor.NextRegenAtUnixSeconds = 0;
            }
            return;
        }

        if (actor.NextRegenAtUnixSeconds <= 0)
        {
            actor.NextRegenAtUnixSeconds = nowUnixSeconds + attacksConfig.RegenSeconds;
        }

        while (actor.RemainingAttacks < attacksConfig.MaxAttacks && nowUnixSeconds >= actor.NextRegenAtUnixSeconds)
        {
            actor.RemainingAttacks++;
            actor.LastRegenUnixSeconds = actor.NextRegenAtUnixSeconds;

            if (actor.RemainingAttacks >= attacksConfig.MaxAttacks)
            {
                actor.RemainingAttacks = attacksConfig.MaxAttacks;
                actor.NextRegenAtUnixSeconds = 0;
                break;
            }

            actor.NextRegenAtUnixSeconds += attacksConfig.RegenSeconds;
        }
    }

    private static long GetDecisionIntervalSeconds(SimulationActorState actor)
    {
        return actor.Kind == SimulationActorKind.SimulatedPlayer
            ? 6 * 60
            : actor.Profile switch
            {
                SimulationActorProfile.Aggressive => 5 * 60,
                SimulationActorProfile.Expansive => 8 * 60,
                _ => 12 * 60
            };
    }

    private static SimulationActorProfile ResolveProfile(int playerId)
    {
        if (playerId == 1) return SimulationActorProfile.Tactical;

        return (Math.Abs(playerId) % 3) switch
        {
            0 => SimulationActorProfile.Aggressive,
            1 => SimulationActorProfile.Expansive,
            _ => SimulationActorProfile.Defensive
        };
    }

    private static CandidateAttack? ChooseAttack(
        SimulationActorState actor,
        long nowUnixSeconds,
        int seed,
        MapService map,
        MapConfig mapConfig,
        GameState state,
        FactionSnapshotService factions,
        MatchmakingService matchmaking,
        BattleSimService battleSim)
    {
        var sectors = state.MapState?.Sectors;
        if (sectors == null || sectors.Length == 0) return null;

        var attacker = factions.BuildCurrentSnapshot(actor.PlayerId);
        CandidateAttack? best = null;

        for (var i = 0; i < sectors.Length; i++)
        {
            var sector = sectors[i];
            if (sector == null) continue;
            if (sector.OwnerPlayerId == actor.PlayerId) continue;
            if (map.IsHomeSector(sector.SectorId)) continue;
            if (!map.HasHomeConnectedOwnedNeighbor(sector.SectorId, actor.PlayerId)) continue;

            if (mapConfig.SectorAttackCooldownSeconds > 0 && sector.LastCombatUnixSeconds > 0)
            {
                var age = nowUnixSeconds - sector.LastCombatUnixSeconds;
                if (age >= 0 && age < mapConfig.SectorAttackCooldownSeconds) continue;
            }

            foreach (var strategy in GetStrategies(actor))
            {
                var modifiers = ComputeModifiers(state, mapConfig, battleSim.Config, map, actor.PlayerId, sector.OwnerPlayerId, sector.SectorId);
                var defender = matchmaking.GetDefenderSnapshot(attacker, sector, MixSeed(seed, sector.SectorId, (int)strategy, actor.PlayerId));
                var preview = battleSim.Simulate(
                    attacker.PvpPower,
                    defender.PvpPower,
                    strategy,
                    sector.Stability,
                    MixSeed(seed, sector.SectorId, actor.PlayerId, 17),
                    flankBonus: modifiers.FlankBonus,
                    defenseBonus: modifiers.DefenseBonus,
                    maintenanceMultiplier: modifiers.MaintenanceMultiplier,
                    underdogBonus: modifiers.UnderdogBonus);

                var score = ScoreAttack(actor, sector, mapConfig, preview.WinChance, strategy);
                if (best == null || score > best.Value.Score)
                {
                    best = new CandidateAttack(actor.PlayerId, sector.SectorId, strategy, score);
                }
            }
        }

        return best;
    }

    private static IEnumerable<AttackStrategy> GetStrategies(SimulationActorState actor)
    {
        if (actor.Kind == SimulationActorKind.SimulatedPlayer)
        {
            yield return AttackStrategy.Stable;
            yield return AttackStrategy.Aggressive;
            yield return AttackStrategy.Risky;
            yield break;
        }

        yield return actor.Profile switch
        {
            SimulationActorProfile.Aggressive => AttackStrategy.Risky,
            SimulationActorProfile.Expansive => AttackStrategy.Stable,
            _ => AttackStrategy.Aggressive
        };
    }

    private static double ScoreAttack(SimulationActorState actor, SectorState sector, MapConfig mapConfig, double winChance, AttackStrategy strategy)
    {
        var productionBonus = 0f;
        for (var i = 0; i < mapConfig.SectorDefinitions.Length; i++)
        {
            var def = mapConfig.SectorDefinitions[i];
            if (def == null || def.SectorId != sector.SectorId) continue;
            productionBonus = def.ProductionBonusPercent;
            break;
        }

        var neutralBonus = sector.OwnerPlayerId == 0 ? 10.0 : 0.0;
        var lowStabilityBonus = (100.0 - sector.Stability) * 0.15;
        var strategyBias = actor.Kind == SimulationActorKind.SimulatedPlayer
            ? strategy switch
            {
                AttackStrategy.Stable => 3.0,
                AttackStrategy.Aggressive => 1.5,
                _ => 0.0
            }
            : 0.0;

        return (winChance * 100.0) + productionBonus + neutralBonus + lowStabilityBonus + strategyBias;
    }

    private static AttackResolution ResolveAttack(
        CandidateAttack attack,
        long nowUnixSeconds,
        int seed,
        MapConfig mapConfig,
        GameState state,
        MapService map,
        FactionSnapshotService factions,
        MatchmakingService matchmaking,
        BattleSimService battleSim)
    {
        var sector = map.GetSector(attack.SectorId);
        if (sector == null) return new AttackResolution(false, false, false);

        var attacker = factions.BuildCurrentSnapshot(attack.PlayerId);
        var defender = matchmaking.GetDefenderSnapshot(attacker, sector, MixSeed(seed, attack.SectorId, attack.PlayerId, 101));
        var modifiers = ComputeModifiers(state, mapConfig, battleSim.Config, map, attack.PlayerId, sector.OwnerPlayerId, attack.SectorId);

        var battle = battleSim.Simulate(
            attacker.PvpPower,
            defender.PvpPower,
            attack.Strategy,
            sector.Stability,
            nowUnixSeconds,
            MixSeed(seed, attack.SectorId, attack.PlayerId, 991),
            flankBonus: modifiers.FlankBonus,
            defenseBonus: modifiers.DefenseBonus,
            maintenanceMultiplier: modifiers.MaintenanceMultiplier,
            underdogBonus: modifiers.UnderdogBonus);

        sector.LastCombatUnixSeconds = nowUnixSeconds;

        if (battle.Win)
        {
            sector.OwnerPlayerId = attack.PlayerId;
            sector.OwnerSnapshot = attacker;
            sector.CapturedUnixSeconds = nowUnixSeconds;
            sector.Stability = mapConfig.GetCaptureStabilityStart(attack.Strategy);
        }
        else
        {
            var next = sector.Stability + mapConfig.StabilityGainOnDefenseWin;
            if (next < 0f) next = 0f;
            if (next > 100f) next = 100f;
            sector.Stability = next;
        }

        return new AttackResolution(
            battle.Win,
            modifiers.UnderdogBonus > 1.0 + 1e-9,
            modifiers.MaintenanceMultiplier < 1.0 - 1e-9);
    }

    private static CombatModifiers ComputeModifiers(
        GameState state,
        MapConfig mapConfig,
        PvpConfig pvpConfig,
        MapService map,
        int attackerPlayerId,
        int defenderPlayerId,
        int targetSectorId)
    {
        var connectedOwned = MapConnectivityService.BuildHomeConnectedSectorSet(state.MapState, mapConfig, attackerPlayerId);

        var adjacentAttackers = 0;
        foreach (var ownedSectorId in connectedOwned)
        {
            if (map.IsAdjacent(ownedSectorId, targetSectorId)) adjacentAttackers++;
        }

        var extraAttackers = Math.Max(0, adjacentAttackers - 1);
        var flankRaw = 1.0 + (pvpConfig.FlankBonusPerExtraAttacker * extraAttackers);
        var flankBonus = Math.Min(pvpConfig.FlankBonusMaxMultiplier, flankRaw);

        var extra = Math.Max(0, connectedOwned.Count - pvpConfig.CombatMaintenanceFreeSectors);
        var penalty = extra * pvpConfig.CombatMaintenancePenaltyPerExtraSector;
        var maintenance = 1.0 - penalty;
        if (maintenance < pvpConfig.CombatMaintenanceMinMultiplier) maintenance = pvpConfig.CombatMaintenanceMinMultiplier;
        if (maintenance > 1.0) maintenance = 1.0;

        var underdog = 1.0;
        if (defenderPlayerId > 0 && defenderPlayerId != attackerPlayerId)
        {
            var defenderCount = MapConnectivityService.BuildHomeConnectedSectorSet(state.MapState, mapConfig, defenderPlayerId).Count;
            var deficit = defenderCount - connectedOwned.Count;
            if (deficit > 0)
            {
                var t = Math.Min(1.0, deficit / (double)pvpConfig.UnderdogSectorDeficitForMaxBonus);
                underdog = 1.0 + (pvpConfig.UnderdogMaxAttackBonus * t);
            }
        }

        return new CombatModifiers(flankBonus, pvpConfig.DefenseBonus, maintenance, underdog);
    }

    private static void Increment(Dictionary<int, int> map, int key, int delta)
    {
        map.TryGetValue(key, out var current);
        map[key] = current + delta;
    }

    private static void AddOwned(Dictionary<int, double> target, Dictionary<int, int> values)
    {
        foreach (var kvp in values)
        {
            target.TryGetValue(kvp.Key, out var current);
            target[kvp.Key] = current + kvp.Value;
        }
    }

    private static void AddShares(Dictionary<int, double> target, Dictionary<int, double> values)
    {
        foreach (var kvp in values)
        {
            target.TryGetValue(kvp.Key, out var current);
            target[kvp.Key] = current + kvp.Value;
        }
    }

    private static void Normalize(Dictionary<int, double> map, int divisor)
    {
        if (divisor <= 0) return;
        var keys = map.Keys.ToArray();
        for (var i = 0; i < keys.Length; i++)
        {
            map[keys[i]] /= divisor;
        }
    }

    private static double Rate(int numerator, int denominator)
    {
        return denominator <= 0 ? 0 : numerator / (double)denominator;
    }

    private static int MixSeed(int a, int b, int c, int d)
    {
        unchecked
        {
            var h = 17;
            h = (h * 31) + a;
            h = (h * 31) + b;
            h = (h * 31) + c;
            h = (h * 31) + d;
            return h;
        }
    }

    private readonly record struct CandidateAttack(int PlayerId, int SectorId, AttackStrategy Strategy, double Score);
    private readonly record struct CombatModifiers(double FlankBonus, double DefenseBonus, double MaintenanceMultiplier, double UnderdogBonus);
    private readonly record struct AttackResolution(bool Win, bool HadUnderdogBonus, bool HadMaintenancePenalty);
}
