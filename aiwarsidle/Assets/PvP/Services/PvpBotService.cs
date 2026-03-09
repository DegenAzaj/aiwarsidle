using System;
using System.Collections.Generic;
using AIWarsIdle.GameCore.Domain;
using AIWarsIdle.GameCore.Services;
using AIWarsIdle.PvP.Config;

namespace AIWarsIdle.PvP.Services
{
    public sealed class PvpBotService
    {
        private const int AggressiveIntervalSeconds = 5 * 60;
        private const int ExpansiveIntervalSeconds = 8 * 60;
        private const int DefensiveIntervalSeconds = 12 * 60;

        private readonly GameState _state;
        private readonly MapService _map;
        private readonly MapConfig _mapConfig;
        private readonly SnapshotService _snapshot;
        private readonly MatchmakingService _matchmaking;
        private readonly BattleSimService _battleSim;
        private readonly FactionSnapshotService _factionSnapshots;
        private readonly PvpAttacksConfig _attacksConfig;
        private readonly IEventBus _eventBus;
        private readonly Dictionary<int, long> _lastDecisionBucketByPlayerId = new();
        private readonly Dictionary<int, BotAttackChargesState> _attackStateByPlayerId = new();

        public PvpBotService(
            GameState state,
            MapService map,
            MapConfig mapConfig,
            SnapshotService snapshot,
            MatchmakingService matchmaking,
            BattleSimService battleSim,
            FactionSnapshotService factionSnapshots,
            PvpAttacksConfig attacksConfig,
            IEventBus eventBus = null)
        {
            _state = state ?? throw new ArgumentNullException(nameof(state));
            _map = map ?? throw new ArgumentNullException(nameof(map));
            _mapConfig = mapConfig ?? throw new ArgumentNullException(nameof(mapConfig));
            _snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
            _matchmaking = matchmaking ?? throw new ArgumentNullException(nameof(matchmaking));
            _battleSim = battleSim ?? throw new ArgumentNullException(nameof(battleSim));
            _factionSnapshots = factionSnapshots ?? throw new ArgumentNullException(nameof(factionSnapshots));
            _attacksConfig = attacksConfig ?? throw new ArgumentNullException(nameof(attacksConfig));
            _eventBus = eventBus;
        }

        public void Tick(long nowUnixSeconds)
        {
            if (nowUnixSeconds < 0) throw new ArgumentOutOfRangeException(nameof(nowUnixSeconds));
            if (!_map.IsInitialized) return;
            if (_map.NeedsSeasonReset(nowUnixSeconds)) return;

            var homeIds = _mapConfig.GetEffectiveHomeSectorIds();
            for (var i = 0; i < homeIds.Length; i++)
            {
                var playerId = _mapConfig.ResolveHomeOwnerPlayerIdByIndex(i);
                if (playerId <= 0 || playerId == _mapConfig.LocalPlayerId) continue;
                TryRunBotTurn(playerId, nowUnixSeconds);
            }
        }

        public void DebugResetMatch(long nowUnixSeconds)
        {
            if (nowUnixSeconds < 0) throw new ArgumentOutOfRangeException(nameof(nowUnixSeconds));

            _lastDecisionBucketByPlayerId.Clear();

            var homeIds = _mapConfig.GetEffectiveHomeSectorIds();
            for (var i = 0; i < homeIds.Length; i++)
            {
                var playerId = _mapConfig.ResolveHomeOwnerPlayerIdByIndex(i);
                if (playerId <= 0 || playerId == _mapConfig.LocalPlayerId) continue;

                var state = GetOrCreateAttackState(playerId);
                state.Remaining = Math.Max(0, _attacksConfig.MaxAttacks);
                state.LastRegenUnixSeconds = 0;
                state.NextRegenAtUnixSeconds = 0;

                var interval = GetDecisionIntervalSeconds(playerId);
                if (interval > 0)
                {
                    _lastDecisionBucketByPlayerId[playerId] = nowUnixSeconds / interval;
                }
            }
        }

        private void TryRunBotTurn(int playerId, long nowUnixSeconds)
        {
            var interval = GetDecisionIntervalSeconds(playerId);
            if (interval <= 0) return;
            TickBotCharges(playerId, nowUnixSeconds);
            if (!CanSpendAttack(playerId)) return;

            var bucket = nowUnixSeconds / interval;
            if (_lastDecisionBucketByPlayerId.TryGetValue(playerId, out var lastBucket) && lastBucket == bucket)
            {
                return;
            }

            _lastDecisionBucketByPlayerId[playerId] = bucket;

            var candidate = ChooseAttack(playerId, nowUnixSeconds, seed: MixSeed(playerId, (int)bucket));
            if (candidate == null) return;

            if (!TrySpendAttack(playerId, nowUnixSeconds)) return;

            ResolveAttack(playerId, candidate.Value.SectorId, candidate.Value.Strategy, nowUnixSeconds, seed: MixSeed(playerId, candidate.Value.SectorId, (int)bucket, 991));
        }

        private AttackChoice? ChooseAttack(int playerId, long nowUnixSeconds, int seed)
        {
            var sectors = _state.MapState?.Sectors;
            if (sectors == null || sectors.Length == 0) return null;

            var profile = GetProfile(playerId);
            var candidates = new List<AttackChoice>(8);

            for (var i = 0; i < sectors.Length; i++)
            {
                var sector = sectors[i];
                if (sector == null) continue;
                if (sector.OwnerPlayerId == playerId) continue;
                if (_map.IsHomeSector(sector.SectorId)) continue;
                if (!_map.HasHomeConnectedOwnedNeighbor(sector.SectorId, playerId)) continue;

                if (_mapConfig.SectorAttackCooldownSeconds > 0 && sector.LastCombatUnixSeconds > 0)
                {
                    var age = nowUnixSeconds - sector.LastCombatUnixSeconds;
                    if (age >= 0 && age < _mapConfig.SectorAttackCooldownSeconds) continue;
                }

                candidates.Add(new AttackChoice
                {
                    SectorId = sector.SectorId,
                    Strategy = ChooseStrategy(profile, sector),
                    Score = ScoreTarget(profile, sector)
                });
            }

            if (candidates.Count == 0) return null;

            candidates.Sort((a, b) => b.Score.CompareTo(a.Score));

            var top = Math.Min(3, candidates.Count);
            var pick = Math.Abs(seed) % top;
            return candidates[pick];
        }

        private void ResolveAttack(int playerId, int sectorId, AttackStrategy strategy, long nowUnixSeconds, int seed)
        {
            var sector = _map.GetSector(sectorId);
            if (sector == null) return;

            var attacker = BuildBotSnapshot(playerId);
            var defender = _matchmaking.GetDefenderSnapshot(attacker, sector, MixSeed(seed, 101), attackerPlayerId: playerId);
            var modifiers = BuildCombatModifiers(playerId, sector.OwnerPlayerId, sectorId);
            var battle = _battleSim.Simulate(
                attacker.PvpPower,
                defender.PvpPower,
                strategy,
                sector.Stability,
                nowUnixSeconds,
                seed,
                flankBonus: modifiers.FlankBonus,
                defenseBonus: modifiers.DefenseBonus,
                maintenanceMultiplier: modifiers.MaintenanceMultiplier,
                underdogBonus: modifiers.UnderdogBonus);

            sector.LastCombatUnixSeconds = nowUnixSeconds;
            _eventBus?.Publish(new SectorAttackEvent(sectorId, strategy));

            if (battle.Win)
            {
                var previousOwner = sector.OwnerPlayerId;
                sector.OwnerPlayerId = playerId;
                sector.OwnerSnapshot = attacker;
                sector.CapturedUnixSeconds = nowUnixSeconds;
                sector.Stability = _mapConfig.GetCaptureStabilityStart(strategy);

                if (previousOwner != playerId)
                {
                    _eventBus?.Publish(new SectorOwnershipChangedEvent(sectorId, previousOwner, playerId));
                }
            }
            else
            {
                var next = sector.Stability + _mapConfig.StabilityGainOnDefenseWin;
                if (next < 0f) next = 0f;
                if (next > 100f) next = 100f;
                sector.Stability = next;
            }

            _eventBus?.Publish(new SectorResultEvent(sectorId, battle.Win));
        }

        private PvpSnapshot BuildBotSnapshot(int playerId)
        {
            return _factionSnapshots.BuildCurrentSnapshot(playerId);
        }

        private int CountOwnedSectors(int playerId)
        {
            return MapConnectivityService.BuildHomeConnectedSectorSet(_state.MapState, _mapConfig, playerId).Count;
        }

        private CombatModifiers BuildCombatModifiers(int attackerPlayerId, int defenderPlayerId, int targetSectorId)
        {
            return new CombatModifiers(
                ComputeFlankBonus(attackerPlayerId, targetSectorId),
                _battleSim.Config.DefenseBonus,
                ComputeMaintenanceMultiplier(attackerPlayerId),
                ComputeUnderdogBonus(attackerPlayerId, defenderPlayerId));
        }

        private double ComputeFlankBonus(int attackerPlayerId, int targetSectorId)
        {
            var connectedOwned = MapConnectivityService.BuildHomeConnectedSectorSet(_state.MapState, _mapConfig, attackerPlayerId);
            if (connectedOwned.Count == 0) return 1.0;

            var adjacentAttackers = 0;
            foreach (var ownedSectorId in connectedOwned)
            {
                if (_map.IsAdjacent(ownedSectorId, targetSectorId)) adjacentAttackers++;
            }

            var extraAttackers = Math.Max(0, adjacentAttackers - 1);
            var raw = 1.0 + (_battleSim.Config.FlankBonusPerExtraAttacker * extraAttackers);
            return Math.Min(_battleSim.Config.FlankBonusMaxMultiplier, raw);
        }

        private double ComputeMaintenanceMultiplier(int attackerPlayerId)
        {
            var ownedConnected = CountOwnedSectors(attackerPlayerId);
            var extra = Math.Max(0, ownedConnected - _battleSim.Config.CombatMaintenanceFreeSectors);
            var penalty = extra * _battleSim.Config.CombatMaintenancePenaltyPerExtraSector;
            var multiplier = 1.0 - penalty;
            if (multiplier < _battleSim.Config.CombatMaintenanceMinMultiplier) multiplier = _battleSim.Config.CombatMaintenanceMinMultiplier;
            if (multiplier > 1.0) multiplier = 1.0;
            return multiplier;
        }

        private double ComputeUnderdogBonus(int attackerPlayerId, int defenderPlayerId)
        {
            if (defenderPlayerId <= 0 || defenderPlayerId == attackerPlayerId) return 1.0;

            var attackerCount = CountOwnedSectors(attackerPlayerId);
            var defenderCount = CountOwnedSectors(defenderPlayerId);
            var deficit = defenderCount - attackerCount;
            if (deficit <= 0) return 1.0;

            var t = Math.Min(1.0, deficit / (double)_battleSim.Config.UnderdogSectorDeficitForMaxBonus);
            return 1.0 + (_battleSim.Config.UnderdogMaxAttackBonus * t);
        }

        private static BotProfile GetProfile(int playerId)
        {
            return (Math.Abs(playerId) % 3) switch
            {
                0 => BotProfile.Aggressive,
                1 => BotProfile.Expansive,
                _ => BotProfile.Defensive
            };
        }

        private static AttackStrategy ChooseStrategy(BotProfile profile, SectorState sector)
        {
            return profile switch
            {
                BotProfile.Aggressive => sector.OwnerPlayerId == 0 ? AttackStrategy.Stable : AttackStrategy.Risky,
                BotProfile.Expansive => sector.OwnerPlayerId == 0 ? AttackStrategy.Aggressive : AttackStrategy.Stable,
                _ => sector.Stability <= 25f ? AttackStrategy.Aggressive : AttackStrategy.Stable
            };
        }

        private static float ScoreTarget(BotProfile profile, SectorState sector)
        {
            var neutralBonus = sector.OwnerPlayerId == 0 ? 30f : 0f;
            var playerBonus = sector.OwnerPlayerId == 1 ? 45f : 0f;
            var lowStability = 100f - sector.Stability;

            return profile switch
            {
                BotProfile.Aggressive => playerBonus + lowStability,
                BotProfile.Expansive => neutralBonus + (lowStability * 0.7f),
                _ => (lowStability * 0.9f) + (sector.OwnerPlayerId == 0 ? 15f : 0f)
            };
        }

        private static long GetDecisionIntervalSeconds(int playerId)
        {
            return GetProfile(playerId) switch
            {
                BotProfile.Aggressive => AggressiveIntervalSeconds,
                BotProfile.Expansive => ExpansiveIntervalSeconds,
                _ => DefensiveIntervalSeconds
            };
        }

        private static int MixSeed(params int[] values)
        {
            unchecked
            {
                var hash = 17;
                for (var i = 0; i < values.Length; i++)
                {
                    hash = (hash * 31) + values[i];
                }

                return hash;
            }
        }

        private enum BotProfile
        {
            Aggressive,
            Expansive,
            Defensive
        }

        private struct AttackChoice
        {
            public int SectorId;
            public AttackStrategy Strategy;
            public float Score;
        }

        private void TickBotCharges(int playerId, long nowUnixSeconds)
        {
            var state = GetOrCreateAttackState(playerId);
            if (_attacksConfig.MaxAttacks <= 0)
            {
                state.Remaining = 0;
                state.NextRegenAtUnixSeconds = 0;
                state.LastRegenUnixSeconds = 0;
                return;
            }

            if (state.Remaining >= _attacksConfig.MaxAttacks)
            {
                if (state.NextRegenAtUnixSeconds > 0 && state.NextRegenAtUnixSeconds <= nowUnixSeconds)
                {
                    state.NextRegenAtUnixSeconds = 0;
                }
                return;
            }

            if (state.NextRegenAtUnixSeconds <= 0)
            {
                state.NextRegenAtUnixSeconds = nowUnixSeconds + _attacksConfig.RegenSeconds;
            }

            while (state.Remaining < _attacksConfig.MaxAttacks && nowUnixSeconds >= state.NextRegenAtUnixSeconds)
            {
                state.Remaining++;
                state.LastRegenUnixSeconds = state.NextRegenAtUnixSeconds;

                if (state.Remaining >= _attacksConfig.MaxAttacks)
                {
                    state.Remaining = _attacksConfig.MaxAttacks;
                    state.NextRegenAtUnixSeconds = 0;
                    break;
                }

                state.NextRegenAtUnixSeconds += _attacksConfig.RegenSeconds;
            }
        }

        private bool CanSpendAttack(int playerId)
        {
            return GetOrCreateAttackState(playerId).Remaining > 0;
        }

        private bool TrySpendAttack(int playerId, long nowUnixSeconds)
        {
            var state = GetOrCreateAttackState(playerId);
            if (state.Remaining <= 0) return false;

            state.Remaining--;
            if (state.Remaining < 0) state.Remaining = 0;

            if (state.Remaining < _attacksConfig.MaxAttacks &&
                (state.NextRegenAtUnixSeconds <= 0 || state.NextRegenAtUnixSeconds <= nowUnixSeconds))
            {
                state.LastRegenUnixSeconds = nowUnixSeconds;
                state.NextRegenAtUnixSeconds = nowUnixSeconds + _attacksConfig.RegenSeconds;
            }

            return true;
        }

        private BotAttackChargesState GetOrCreateAttackState(int playerId)
        {
            if (_attackStateByPlayerId.TryGetValue(playerId, out var existing))
            {
                return existing;
            }

            var created = new BotAttackChargesState
            {
                Remaining = Math.Max(0, _attacksConfig.MaxAttacks),
                LastRegenUnixSeconds = 0,
                NextRegenAtUnixSeconds = 0
            };
            _attackStateByPlayerId.Add(playerId, created);
            return created;
        }

        private sealed class BotAttackChargesState
        {
            public int Remaining;
            public long LastRegenUnixSeconds;
            public long NextRegenAtUnixSeconds;
        }

        private readonly struct CombatModifiers
        {
            public readonly double FlankBonus;
            public readonly double DefenseBonus;
            public readonly double MaintenanceMultiplier;
            public readonly double UnderdogBonus;

            public CombatModifiers(double flankBonus, double defenseBonus, double maintenanceMultiplier, double underdogBonus)
            {
                FlankBonus = flankBonus;
                DefenseBonus = defenseBonus;
                MaintenanceMultiplier = maintenanceMultiplier;
                UnderdogBonus = underdogBonus;
            }
        }
    }
}
