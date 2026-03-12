using System;
using System.Collections.Generic;

namespace AIWarsIdle.PvP.Services
{
    public sealed class HexHackDuelService
    {
        public const float MatchDurationSeconds = 30f;
        public const float TickIntervalSeconds = 0.1f;
        public const float BasePushPerSecond = 25f;
        public const float FlankBonusPerExtraAttacker = 0.25f;
        public const float DefenseBonusPerFriendlyNeighbor = 0.20f;
        public const float CorePushBonus = 0.30f;
        public const float OverdriveStartsAtSeconds = 25f;
        public const float OverdriveMultiplier = 2f;
        private const int NeutralOwnerId = 0;
        private const int PlayerOwnerId = 1;
        private const int EnemyOwnerId = 2;
        private const double MinPowerRatioClamp = 0.7d;
        private const double MaxPowerRatioClamp = 1.3d;

        private static readonly DuelNodeDefinition[] NodeDefinitions =
        {
            new(0, "A", new DuelCoord(-1, 1), false, NeutralOwnerId, 0f),
            new(1, "B", new DuelCoord(0, 1), false, NeutralOwnerId, 0f),
            new(2, "C", new DuelCoord(-1, 0), false, PlayerOwnerId, 100f),
            new(3, "D", new DuelCoord(0, 0), true, NeutralOwnerId, 0f),
            new(4, "E", new DuelCoord(1, 0), false, EnemyOwnerId, -100f),
            new(5, "F", new DuelCoord(0, -1), false, NeutralOwnerId, 0f),
            new(6, "G", new DuelCoord(1, -1), false, NeutralOwnerId, 0f),
        };

        private static readonly int[][] Neighbors =
        {
            new[] { 1, 2, 3 },
            new[] { 0, 3, 4 },
            new[] { 0, 3, 5 },
            new[] { 0, 1, 2, 4, 5, 6 },
            new[] { 1, 3, 6 },
            new[] { 2, 3, 6 },
            new[] { 3, 4, 5 }
        };

        private readonly SnapshotService _snapshotService;
        private readonly DuelNodeState[] _nodes;
        private readonly DuelLinkState[] _playerLinks;
        private readonly DuelLinkState[] _enemyLinks;
        private readonly Random _seedRng;
        private float _remainingSeconds;
        private float _decisionDelaySeconds;
        private float _decisionCarrySeconds;
        private float _tickCarrySeconds;
        public HexHackDuelService(SnapshotService snapshotService = null, int seed = 1337)
        {
            _snapshotService = snapshotService;
            _seedRng = new Random(seed);
            _nodes = new DuelNodeState[NodeDefinitions.Length];
            _playerLinks = new DuelLinkState[NodeDefinitions.Length];
            _enemyLinks = new DuelLinkState[NodeDefinitions.Length];
            ResetBoard();
        }

        public bool IsMatchActive { get; private set; }
        public bool IsMatchFinished { get; private set; }
        public float RemainingSeconds => _remainingSeconds;
        public float ElapsedSeconds => MatchDurationSeconds - _remainingSeconds;
        public double PlayerPower { get; private set; }
        public double EnemyPower { get; private set; }
        public DuelDifficulty Difficulty { get; private set; } = DuelDifficulty.Medium;
        public string ResultSummary { get; private set; } = "Press PLAY to start a duel.";
        public IReadOnlyList<DuelNodeState> Nodes => _nodes;
        public IReadOnlyList<DuelLinkState> PlayerLinks => _playerLinks;
        public IReadOnlyList<DuelLinkState> EnemyLinks => _enemyLinks;

        public int PlayerHexCount => CountOwned(PlayerOwnerId);
        public int EnemyHexCount => CountOwned(EnemyOwnerId);

        public void StartMatch()
        {
            ResetBoard();
            var localPower = _snapshotService?.BuildSnapshot().PvpPower ?? 100d;
            PlayerPower = Math.Max(1d, localPower);
            EnemyPower = Math.Max(1d, ResolveEnemyPower(PlayerPower, Difficulty));
            _remainingSeconds = MatchDurationSeconds;
            _decisionCarrySeconds = 0f;
            _tickCarrySeconds = 0f;
            _decisionDelaySeconds = NextDecisionDelay();
            ResultSummary = "Match in progress.";
            IsMatchActive = true;
            IsMatchFinished = false;
        }

        public void CycleDifficulty()
        {
            Difficulty = Difficulty switch
            {
                DuelDifficulty.Easy => DuelDifficulty.Medium,
                DuelDifficulty.Medium => DuelDifficulty.Hard,
                _ => DuelDifficulty.Easy
            };
        }

        public void Tick(float deltaSeconds)
        {
            if (!IsMatchActive || deltaSeconds <= 0f) return;

            _remainingSeconds = Math.Max(0f, _remainingSeconds - deltaSeconds);
            _decisionCarrySeconds += deltaSeconds;
            _tickCarrySeconds += deltaSeconds;

            while (_decisionCarrySeconds >= _decisionDelaySeconds && IsMatchActive)
            {
                _decisionCarrySeconds -= _decisionDelaySeconds;
                RebuildEnemyLinks();
                _decisionDelaySeconds = NextDecisionDelay();
            }

            while (_tickCarrySeconds >= TickIntervalSeconds && IsMatchActive)
            {
                _tickCarrySeconds -= TickIntervalSeconds;
                SimulateTick(TickIntervalSeconds);
            }

            if (_remainingSeconds <= 0f && IsMatchActive)
            {
                FinishMatch();
            }
        }

        public DuelCommandResult TryCreateOrMovePlayerLink(int sourceId, int targetId)
        {
            if (!IsMatchActive)
            {
                return DuelCommandResult.Fail("Press PLAY to start.");
            }

            if (!TryGetNode(sourceId, out var source) || !TryGetNode(targetId, out var target))
            {
                return DuelCommandResult.Fail("Invalid node.");
            }

            if (source.OwnerPlayerId != PlayerOwnerId || source.Control < 100f)
            {
                return DuelCommandResult.Fail("Source must be fully controlled by you.");
            }

            if (!CanTargetNodeForPressure(target, PlayerOwnerId))
            {
                return DuelCommandResult.Fail("Pick a neutral, enemy, or damaged allied node.");
            }

            if (!IsAdjacent(sourceId, targetId))
            {
                return DuelCommandResult.Fail("Target must be adjacent.");
            }

            if (SetOrToggleLink(_playerLinks, sourceId, targetId))
            {
                return DuelCommandResult.Ok($"Link {GetLabel(sourceId)} -> {GetLabel(targetId)} updated.");
            }

            return DuelCommandResult.Fail("No free link slots. Clear one source first.");
        }

        public DuelCommandResult ClearPlayerLink(int sourceId)
        {
            if (RemoveLinksFromSource(_playerLinks, sourceId) > 0)
            {
                return DuelCommandResult.Ok($"Links from {GetLabel(sourceId)} cleared.");
            }

            return DuelCommandResult.Fail("That node has no active link.");
        }

        public void ClearAllPlayerLinks()
        {
            Array.Clear(_playerLinks, 0, _playerLinks.Length);
        }

        public DuelCommandResult AutoAssignPlayerLinksToTarget(int targetId)
        {
            if (!IsMatchActive)
            {
                return DuelCommandResult.Fail("Press PLAY to start.");
            }

            if (!TryGetNode(targetId, out var target))
            {
                return DuelCommandResult.Fail("Invalid node.");
            }

            if (!CanTargetNodeForPressure(target, PlayerOwnerId))
            {
                return DuelCommandResult.Fail("Target a neutral, enemy, or damaged allied node.");
            }

            var sources = CollectAutoLinkSources(targetId);
            if (sources.Count == 0)
            {
                return DuelCommandResult.Fail("No adjacent fully controlled player nodes.");
            }

            Array.Clear(_playerLinks, 0, _playerLinks.Length);
            var assignedCount = sources.Count;
            for (var i = 0; i < assignedCount; i++)
            {
                _playerLinks[i] = new DuelLinkState(sources[i], targetId, PlayerOwnerId);
            }

            return DuelCommandResult.Ok($"Linked {assignedCount} node(s) into {GetLabel(targetId)}.");
        }

        public bool IsAdjacent(int a, int b)
        {
            if (a < 0 || a >= Neighbors.Length) return false;
            var neighbors = Neighbors[a];
            for (var i = 0; i < neighbors.Length; i++)
            {
                if (neighbors[i] == b) return true;
            }

            return false;
        }

        public string GetLabel(int nodeId)
        {
            return nodeId >= 0 && nodeId < NodeDefinitions.Length ? NodeDefinitions[nodeId].Label : "?";
        }

        public static bool IsPlayerOwner(int ownerPlayerId) => ownerPlayerId == PlayerOwnerId;
        public static bool IsEnemyOwner(int ownerPlayerId) => ownerPlayerId == EnemyOwnerId;

        private List<int> CollectAutoLinkSources(int targetId)
        {
            return CollectAutoLinkSources(targetId, PlayerOwnerId);
        }

        private List<int> CollectAutoLinkSources(int targetId, int ownerPlayerId)
        {
            var sources = new List<int>(_playerLinks.Length);
            var neighbors = Neighbors[targetId];
            for (var i = 0; i < neighbors.Length; i++)
            {
                var sourceId = neighbors[i];
                var node = _nodes[sourceId];
                if (node.OwnerPlayerId != ownerPlayerId || !IsFullyControlledByOwner(node, ownerPlayerId)) continue;
                sources.Add(sourceId);
            }

            sources.Sort((a, b) =>
            {
                var aCore = _nodes[a].IsCore ? 1 : 0;
                var bCore = _nodes[b].IsCore ? 1 : 0;
                var byCore = bCore.CompareTo(aCore);
                return byCore != 0 ? byCore : a.CompareTo(b);
            });

            return sources;
        }

        private void SimulateTick(float dt)
        {
            PruneInvalidLinks(_playerLinks, PlayerOwnerId);
            PruneInvalidLinks(_enemyLinks, EnemyOwnerId);

            var playerPush = new float[_nodes.Length];
            var enemyPush = new float[_nodes.Length];

            AccumulatePush(_playerLinks, PlayerOwnerId, playerPush, EnemyPower <= 0d ? 1d : Math.Clamp(PlayerPower / EnemyPower, MinPowerRatioClamp, MaxPowerRatioClamp));
            AccumulatePush(_enemyLinks, EnemyOwnerId, enemyPush, PlayerPower <= 0d ? 1d : Math.Clamp(EnemyPower / PlayerPower, MinPowerRatioClamp, MaxPowerRatioClamp));

            for (var i = 0; i < _nodes.Length; i++)
            {
                var node = _nodes[i];
                var next = node.Control + ((playerPush[i] - enemyPush[i]) * dt);
                _nodes[i] = node.WithState(Math.Clamp(next, -100f, 100f), node.OwnerPlayerId);
            }

            ResolveCaptures();
        }

        private void ResolveCaptures()
        {
            for (var i = 0; i < _nodes.Length; i++)
            {
                var node = _nodes[i];
                var previousOwner = node.OwnerPlayerId;
                var nextOwner = node.OwnerPlayerId;
                var nextControl = node.Control;

                if (node.Control >= 100f)
                {
                    nextOwner = PlayerOwnerId;
                    nextControl = 100f;
                }
                else if (node.Control <= -100f)
                {
                    nextOwner = EnemyOwnerId;
                    nextControl = -100f;
                }
                else if (Math.Abs(node.Control) < 0.001f)
                {
                    nextOwner = NeutralOwnerId;
                    nextControl = 0f;
                }

                _nodes[i] = node.WithState(nextControl, nextOwner);

                if (previousOwner != nextOwner)
                {
                    RemoveLinksFromSource(_playerLinks, node.Id);
                    RemoveLinksFromSource(_enemyLinks, node.Id);
                }
            }
        }

        private void AccumulatePush(DuelLinkState[] links, int attackerOwnerId, float[] output, double powerMultiplier)
        {
            for (var targetId = 0; targetId < _nodes.Length; targetId++)
            {
                var attackers = CollectAttackers(links, targetId, attackerOwnerId);
                if (attackers.Count == 0) continue;

                var flankMultiplier = 1f + (FlankBonusPerExtraAttacker * (attackers.Count - 1));
                var defenseMultiplier = GetDefenseMultiplier(targetId);

                for (var i = 0; i < attackers.Count; i++)
                {
                    var sourceId = attackers[i];
                    var push = BasePushPerSecond * flankMultiplier;

                    if (_nodes[sourceId].IsCore && _nodes[sourceId].OwnerPlayerId == attackerOwnerId)
                    {
                        push *= 1f + CorePushBonus;
                    }

                    push *= (float)powerMultiplier;

                    if (ElapsedSeconds >= OverdriveStartsAtSeconds)
                    {
                        push *= OverdriveMultiplier;
                    }

                    output[targetId] += push / defenseMultiplier;
                }
            }
        }

        private List<int> CollectAttackers(DuelLinkState[] links, int targetId, int attackerOwnerId)
        {
            var result = new List<int>(_playerLinks.Length);
            for (var i = 0; i < links.Length; i++)
            {
                var link = links[i];
                if (!link.IsActive || link.TargetId != targetId) continue;
                if (_nodes[link.SourceId].OwnerPlayerId != attackerOwnerId) continue;
                result.Add(link.SourceId);
            }

            return result;
        }

        private float GetDefenseMultiplier(int targetId)
        {
            var owner = _nodes[targetId].OwnerPlayerId;
            if (owner == NeutralOwnerId) return 1f;

            var friendlyNeighbors = 0;
            var neighbors = Neighbors[targetId];
            for (var i = 0; i < neighbors.Length; i++)
            {
                if (_nodes[neighbors[i]].OwnerPlayerId == owner)
                {
                    friendlyNeighbors++;
                }
            }

            return 1f + (DefenseBonusPerFriendlyNeighbor * friendlyNeighbors);
        }

        private void RebuildEnemyLinks()
        {
            Array.Clear(_enemyLinks, 0, _enemyLinks.Length);

            var bestTargetId = -1;
            var bestScore = float.MinValue;
            for (var targetId = 0; targetId < _nodes.Length; targetId++)
            {
                var target = _nodes[targetId];
                if (!CanTargetNodeForPressure(target, EnemyOwnerId)) continue;

                var sources = CollectAutoLinkSources(targetId, EnemyOwnerId);
                if (sources.Count == 0) continue;

                var score = ScoreEnemyCandidate(targetId);
                if (score <= bestScore) continue;

                bestScore = score;
                bestTargetId = targetId;
            }

            if (bestTargetId < 0) return;

            var selectedSources = CollectAutoLinkSources(bestTargetId, EnemyOwnerId);
            for (var i = 0; i < selectedSources.Count; i++)
            {
                _enemyLinks[i] = new DuelLinkState(selectedSources[i], bestTargetId, EnemyOwnerId);
            }
        }

        private float ScoreEnemyCandidate(int targetId)
        {
            var node = _nodes[targetId];
            var score = (float)_seedRng.NextDouble();

            if (node.IsCore)
            {
                score += 1000f;
            }

            if (node.OwnerPlayerId == NeutralOwnerId)
            {
                score += 50f;
            }

            score += Math.Clamp(-node.Control, 0f, 100f) * 2f;

            var enemyAdjacentCount = 0;
            var neighbors = Neighbors[targetId];
            for (var i = 0; i < neighbors.Length; i++)
            {
                if (_nodes[neighbors[i]].OwnerPlayerId == EnemyOwnerId)
                {
                    enemyAdjacentCount++;
                }
            }

            if (enemyAdjacentCount >= 2)
            {
                score += 120f;
            }

            return score;
        }

        private bool SetOrToggleLink(DuelLinkState[] links, int sourceId, int targetId)
        {
            for (var i = 0; i < links.Length; i++)
            {
                if (!links[i].IsActive || links[i].SourceId != sourceId) continue;

                if (links[i].TargetId == targetId)
                {
                    links[i] = default;
                }
                else
                {
                    links[i] = new DuelLinkState(sourceId, targetId, PlayerOwnerId);
                }

                return true;
            }

            for (var i = 0; i < links.Length; i++)
            {
                if (links[i].IsActive) continue;
                links[i] = new DuelLinkState(sourceId, targetId, PlayerOwnerId);
                return true;
            }

            return false;
        }

        private void PruneInvalidLinks(DuelLinkState[] links, int ownerPlayerId)
        {
            for (var i = 0; i < links.Length; i++)
            {
                var link = links[i];
                if (!link.IsActive) continue;

                if (_nodes[link.SourceId].OwnerPlayerId != ownerPlayerId || !IsFullyControlledByOwner(_nodes[link.SourceId], ownerPlayerId))
                {
                    links[i] = default;
                    continue;
                }

                if (!CanTargetNodeForPressure(_nodes[link.TargetId], ownerPlayerId) || !IsAdjacent(link.SourceId, link.TargetId))
                {
                    links[i] = default;
                }
            }
        }

        private static bool CanTargetNodeForPressure(DuelNodeState node, int ownerPlayerId)
        {
            if (node.OwnerPlayerId != ownerPlayerId)
            {
                return true;
            }

            return !IsFullyControlledByOwner(node, ownerPlayerId);
        }

        private static bool IsFullyControlledByOwner(DuelNodeState node, int ownerPlayerId)
        {
            return ownerPlayerId switch
            {
                PlayerOwnerId => node.Control >= 100f,
                EnemyOwnerId => node.Control <= -100f,
                _ => false
            };
        }

        private int RemoveLinksFromSource(DuelLinkState[] links, int sourceId)
        {
            var removed = 0;
            for (var i = 0; i < links.Length; i++)
            {
                if (!links[i].IsActive || links[i].SourceId != sourceId) continue;
                links[i] = default;
                removed++;
            }

            return removed;
        }

        private void FinishMatch()
        {
            IsMatchActive = false;
            IsMatchFinished = true;
            Array.Clear(_playerLinks, 0, _playerLinks.Length);
            Array.Clear(_enemyLinks, 0, _enemyLinks.Length);

            var playerCount = PlayerHexCount;
            var enemyCount = EnemyHexCount;
            if (playerCount > enemyCount)
            {
                ResultSummary = $"Victory {playerCount}:{enemyCount}";
                return;
            }

            if (enemyCount > playerCount)
            {
                ResultSummary = $"Defeat {playerCount}:{enemyCount}";
                return;
            }

            var coreOwner = _nodes[3].OwnerPlayerId;
            ResultSummary = coreOwner switch
            {
                PlayerOwnerId => $"Victory by CORE {playerCount}:{enemyCount}",
                EnemyOwnerId => $"Defeat by CORE {playerCount}:{enemyCount}",
                _ => $"Draw {playerCount}:{enemyCount}"
            };
        }

        private void ResetBoard()
        {
            for (var i = 0; i < NodeDefinitions.Length; i++)
            {
                var def = NodeDefinitions[i];
                _nodes[i] = new DuelNodeState(def.Id, def.Label, def.Coord, def.IsCore, def.StartOwnerPlayerId, def.StartControl);
            }

            Array.Clear(_playerLinks, 0, _playerLinks.Length);
            Array.Clear(_enemyLinks, 0, _enemyLinks.Length);
            PlayerPower = 100d;
            EnemyPower = 100d;
            _remainingSeconds = MatchDurationSeconds;
            IsMatchActive = false;
            IsMatchFinished = false;
            ResultSummary = "Press PLAY to start a duel.";
        }

        private float NextDecisionDelay()
        {
            return 1.5f + ((float)_seedRng.NextDouble() * 1.0f);
        }

        private static double ResolveEnemyPower(double playerPower, DuelDifficulty difficulty)
        {
            var safePlayerPower = Math.Max(1d, playerPower);
            return difficulty switch
            {
                DuelDifficulty.Easy => safePlayerPower * MinPowerRatioClamp,
                DuelDifficulty.Hard => safePlayerPower * MaxPowerRatioClamp,
                _ => safePlayerPower
            };
        }

        private int CountOwned(int ownerPlayerId)
        {
            var count = 0;
            for (var i = 0; i < _nodes.Length; i++)
            {
                if (_nodes[i].OwnerPlayerId == ownerPlayerId)
                {
                    count++;
                }
            }

            return count;
        }

        private bool TryGetNode(int nodeId, out DuelNodeState node)
        {
            if (nodeId >= 0 && nodeId < _nodes.Length)
            {
                node = _nodes[nodeId];
                return true;
            }

            node = default;
            return false;
        }

        private readonly struct DuelNodeDefinition
        {
            public readonly int Id;
            public readonly string Label;
            public readonly DuelCoord Coord;
            public readonly bool IsCore;
            public readonly int StartOwnerPlayerId;
            public readonly float StartControl;

            public DuelNodeDefinition(int id, string label, DuelCoord coord, bool isCore, int startOwnerPlayerId, float startControl)
            {
                Id = id;
                Label = label;
                Coord = coord;
                IsCore = isCore;
                StartOwnerPlayerId = startOwnerPlayerId;
                StartControl = startControl;
            }
        }

        private readonly struct DuelAiCandidate
        {
            public readonly int SourceId;
            public readonly int TargetId;
            public readonly float Score;

            public DuelAiCandidate(int sourceId, int targetId, float score)
            {
                SourceId = sourceId;
                TargetId = targetId;
                Score = score;
            }
        }
    }

    public enum DuelDifficulty
    {
        Easy = 0,
        Medium = 1,
        Hard = 2
    }

    public readonly struct DuelNodeState
    {
        public readonly int Id;
        public readonly string Label;
        public readonly DuelCoord Coord;
        public readonly bool IsCore;
        public readonly int OwnerPlayerId;
        public readonly float Control;

        public DuelNodeState(int id, string label, DuelCoord coord, bool isCore, int ownerPlayerId, float control)
        {
            Id = id;
            Label = label ?? string.Empty;
            Coord = coord;
            IsCore = isCore;
            OwnerPlayerId = ownerPlayerId;
            Control = control;
        }

        public DuelNodeState WithState(float control, int ownerPlayerId)
        {
            return new DuelNodeState(Id, Label, Coord, IsCore, ownerPlayerId, control);
        }
    }

    public readonly struct DuelLinkState
    {
        public readonly int SourceId;
        public readonly int TargetId;
        public readonly int OwnerPlayerId;

        public DuelLinkState(int sourceId, int targetId, int ownerPlayerId)
        {
            SourceId = sourceId;
            TargetId = targetId;
            OwnerPlayerId = ownerPlayerId;
        }

        public bool IsActive => OwnerPlayerId > 0 && SourceId >= 0 && TargetId >= 0;
    }

    public readonly struct DuelCommandResult
    {
        public readonly bool Success;
        public readonly string Message;

        private DuelCommandResult(bool success, string message)
        {
            Success = success;
            Message = message ?? string.Empty;
        }

        public static DuelCommandResult Ok(string message) => new(true, message);
        public static DuelCommandResult Fail(string message) => new(false, message);
    }

    public readonly struct DuelCoord
    {
        public readonly int Q;
        public readonly int R;

        public DuelCoord(int q, int r)
        {
            Q = q;
            R = r;
        }
    }
}
