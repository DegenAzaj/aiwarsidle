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
        private readonly IEventBus _eventBus;
        private readonly Dictionary<int, long> _lastDecisionBucketByPlayerId = new();

        public PvpBotService(
            GameState state,
            MapService map,
            MapConfig mapConfig,
            SnapshotService snapshot,
            MatchmakingService matchmaking,
            BattleSimService battleSim,
            IEventBus eventBus = null)
        {
            _state = state ?? throw new ArgumentNullException(nameof(state));
            _map = map ?? throw new ArgumentNullException(nameof(map));
            _mapConfig = mapConfig ?? throw new ArgumentNullException(nameof(mapConfig));
            _snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
            _matchmaking = matchmaking ?? throw new ArgumentNullException(nameof(matchmaking));
            _battleSim = battleSim ?? throw new ArgumentNullException(nameof(battleSim));
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

        private void TryRunBotTurn(int playerId, long nowUnixSeconds)
        {
            var interval = GetDecisionIntervalSeconds(playerId);
            if (interval <= 0) return;

            var bucket = nowUnixSeconds / interval;
            if (_lastDecisionBucketByPlayerId.TryGetValue(playerId, out var lastBucket) && lastBucket == bucket)
            {
                return;
            }

            _lastDecisionBucketByPlayerId[playerId] = bucket;

            var candidate = ChooseAttack(playerId, nowUnixSeconds, seed: MixSeed(playerId, (int)bucket));
            if (candidate == null) return;

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
                if (!_map.HasOwnedNeighbor(sector.SectorId, playerId)) continue;

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
            var defender = _matchmaking.GetDefenderSnapshot(attacker, sector, MixSeed(seed, 101));
            var battle = _battleSim.Simulate(attacker.PvpPower, defender.PvpPower, strategy, sector.Stability, nowUnixSeconds, seed);

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
            var baseline = _snapshot.BuildSnapshot();
            var ownedCount = CountOwnedSectors(playerId);
            var ownerKey = Math.Max(0, playerId - _mapConfig.LocalPlayerId);
            var multiplier = 0.82 + (ownerKey * 0.06) + (ownedCount * 0.015);
            var power = baseline.PvpPower * multiplier;
            if (double.IsNaN(power) || double.IsInfinity(power) || power < 0) power = 0;

            return new PvpSnapshot
            {
                PvpPower = power,
                League = 0,
                SeasonPoints = 0
            };
        }

        private int CountOwnedSectors(int playerId)
        {
            var count = 0;
            var sectors = _state.MapState?.Sectors;
            if (sectors == null) return 0;

            for (var i = 0; i < sectors.Length; i++)
            {
                if (sectors[i]?.OwnerPlayerId == playerId) count++;
            }

            return count;
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
    }
}
