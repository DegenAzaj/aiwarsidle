using System;
using System.Collections.Generic;
using AIWarsIdle.PvP.Config;
using UnityEngine;

namespace AIWarsIdle.PvP.Services
{
    public sealed class HexHackDuelService
    {
        private static readonly PvpConfig.HexHackDuelCombatSection DefaultCombatConfig = new();
        private const int CoreNodeId = 3;
        private const int NeutralOwnerId = 0;
        private const int PlayerOwnerId = 1;
        private const int EnemyOwnerId = 2;

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
        private readonly PvpConfig.HexHackDuelCombatSection _combatConfig;
        private readonly DuelNodeState[] _nodes;
        private readonly DuelLinkState[] _playerLinks;
        private readonly DuelLinkState[] _enemyLinks;
        private readonly List<string> _playerActionLog = new();
        private readonly List<string> _enemyActionLog = new();
        private readonly System.Random _seedRng;
        private float _remainingSeconds;
        private float _decisionDelaySeconds;
        private float _decisionCarrySeconds;
        private float _tickCarrySeconds;
        private int _enemyLastDecisionTargetId = -1;
        private int _enemySameTargetDecisionStreak;
        public HexHackDuelService(SnapshotService snapshotService = null, PvpConfig pvpConfig = null, int seed = 1337)
        {
            _snapshotService = snapshotService;
            _combatConfig = pvpConfig?.HexHackDuelCombat ?? DefaultCombatConfig;
            _seedRng = new System.Random(seed);
            _nodes = new DuelNodeState[NodeDefinitions.Length];
            _playerLinks = new DuelLinkState[NodeDefinitions.Length];
            _enemyLinks = new DuelLinkState[NodeDefinitions.Length];
            ResetBoard();
        }

        public bool IsMatchActive { get; private set; }
        public bool IsMatchFinished { get; private set; }
        public float RemainingSeconds => _remainingSeconds;
        public float ElapsedSeconds => MatchDurationSeconds - _remainingSeconds;
        public float MatchDurationSeconds => _combatConfig.MatchDurationSeconds;
        public float OverdriveStartsAtSeconds => _combatConfig.OverdriveStartsAtSeconds;
        public float AttackControlThreshold => _combatConfig.AttackControlThreshold;
        public HexHackDuelAttackMode AttackMode => _combatConfig.AttackMode;
        public int MaxConcurrentTargets => _combatConfig.MaxConcurrentTargets;
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
            _decisionDelaySeconds = ResolveInitialDecisionDelay();
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

            while (_tickCarrySeconds >= _combatConfig.TickIntervalSeconds && IsMatchActive)
            {
                _tickCarrySeconds -= _combatConfig.TickIntervalSeconds;
                SimulateTick(_combatConfig.TickIntervalSeconds);
                if (HasFullBoardControl(PlayerOwnerId) || HasFullBoardControl(EnemyOwnerId) || !HasOperationalHex(PlayerOwnerId) || !HasOperationalHex(EnemyOwnerId))
                {
                    FinishMatch();
                    break;
                }
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
                return LogPlayerCommand("link", $"source={GetLabel(sourceId)} target={GetLabel(targetId)}", DuelCommandResult.Fail("Press PLAY to start."));
            }

            if (!TryGetNode(sourceId, out var source) || !TryGetNode(targetId, out var target))
            {
                return LogPlayerCommand("link", $"source={GetLabel(sourceId)} target={GetLabel(targetId)}", DuelCommandResult.Fail("Invalid node."));
            }

            if (source.OwnerPlayerId != PlayerOwnerId || !CanAttackFromNode(source, PlayerOwnerId))
            {
                return LogPlayerCommand("link", $"source={GetLabel(sourceId)} target={GetLabel(targetId)}", DuelCommandResult.Fail($"Source must stay above {_combatConfig.AttackControlThreshold:0} control."));
            }

            if (!CanTargetNodeForPressure(target, PlayerOwnerId))
            {
                return LogPlayerCommand("link", $"source={GetLabel(sourceId)} target={GetLabel(targetId)}", DuelCommandResult.Fail("Pick a neutral, enemy, or damaged allied node."));
            }

            if (!IsAdjacent(sourceId, targetId))
            {
                return LogPlayerCommand("link", $"source={GetLabel(sourceId)} target={GetLabel(targetId)}", DuelCommandResult.Fail("Target must be adjacent."));
            }

            if (SetOrToggleLink(_playerLinks, sourceId, targetId))
            {
                return LogPlayerCommand("link", $"source={GetLabel(sourceId)} target={GetLabel(targetId)}", DuelCommandResult.Ok($"Link {GetLabel(sourceId)} -> {GetLabel(targetId)} updated."));
            }

            return LogPlayerCommand("link", $"source={GetLabel(sourceId)} target={GetLabel(targetId)}", DuelCommandResult.Fail("No free link slots. Clear one source first."));
        }

        public DuelCommandResult ClearPlayerLink(int sourceId)
        {
            if (RemoveLinksFromSource(_playerLinks, sourceId) > 0)
            {
                return LogPlayerCommand("clear", $"source={GetLabel(sourceId)}", DuelCommandResult.Ok($"Links from {GetLabel(sourceId)} cleared."));
            }

            return LogPlayerCommand("clear", $"source={GetLabel(sourceId)}", DuelCommandResult.Fail("That node has no active link."));
        }

        public DuelCommandResult CancelPlayerLinkFromSource(int sourceId)
        {
            if (!IsMatchActive)
            {
                return DuelCommandResult.Fail("Press PLAY to start.");
            }

            return ClearPlayerLink(sourceId);
        }

        public void ClearAllPlayerLinks()
        {
            Array.Clear(_playerLinks, 0, _playerLinks.Length);
        }

        public bool HasPlayerLinkFromSource(int sourceId)
        {
            if (sourceId < 0 || sourceId >= _playerLinks.Length) return false;
            var link = _playerLinks[sourceId];
            return link.IsActive && link.SourceId == sourceId;
        }

        public DuelCommandResult AutoAssignPlayerLinksToTarget(int targetId)
        {
            if (!IsMatchActive)
            {
                return LogPlayerCommand("auto-target", $"target={GetLabel(targetId)}", DuelCommandResult.Fail("Press PLAY to start."));
            }

            if (!TryGetNode(targetId, out var target))
            {
                return LogPlayerCommand("auto-target", $"target={GetLabel(targetId)}", DuelCommandResult.Fail("Invalid node."));
            }

            if (!CanTargetNodeForPressure(target, PlayerOwnerId))
            {
                return LogPlayerCommand("auto-target", $"target={GetLabel(targetId)}", DuelCommandResult.Fail("Target a neutral, enemy, or damaged allied node."));
            }

            var sources = CollectAutoLinkSources(targetId);
            if (sources.Count == 0)
            {
                return LogPlayerCommand("auto-target", $"target={GetLabel(targetId)}", DuelCommandResult.Fail("No adjacent player nodes with enough control."));
            }

            Array.Clear(_playerLinks, 0, _playerLinks.Length);
            var assignedCount = sources.Count;
            for (var i = 0; i < assignedCount; i++)
            {
                _playerLinks[i] = new DuelLinkState(sources[i], targetId, PlayerOwnerId);
            }

            return LogPlayerCommand("auto-target", $"target={GetLabel(targetId)} sources={FormatSources(sources)}", DuelCommandResult.Ok($"Linked {assignedCount} node(s) into {GetLabel(targetId)}."));
        }

        public DuelCommandResult TryAssignPlayerLinkFromSource(int sourceId, int targetId)
        {
            if (!IsMatchActive)
            {
                return LogPlayerCommand("swipe-link", $"source={GetLabel(sourceId)} target={GetLabel(targetId)}", DuelCommandResult.Fail("Press PLAY to start."));
            }

            if (!TryGetNode(sourceId, out var source) || !TryGetNode(targetId, out var target))
            {
                return LogPlayerCommand("swipe-link", $"source={GetLabel(sourceId)} target={GetLabel(targetId)}", DuelCommandResult.Fail("Invalid node."));
            }

            if (source.OwnerPlayerId != PlayerOwnerId || !CanAttackFromNode(source, PlayerOwnerId))
            {
                return LogPlayerCommand("swipe-link", $"source={GetLabel(sourceId)} target={GetLabel(targetId)}", DuelCommandResult.Fail($"Swipe from a player node above {_combatConfig.AttackControlThreshold:0} control."));
            }

            if (!CanTargetNodeForPressure(target, PlayerOwnerId))
            {
                return LogPlayerCommand("swipe-link", $"source={GetLabel(sourceId)} target={GetLabel(targetId)}", DuelCommandResult.Fail("Pick a neutral, enemy, or damaged allied node."));
            }

            if (!IsAdjacent(sourceId, targetId))
            {
                return LogPlayerCommand("swipe-link", $"source={GetLabel(sourceId)} target={GetLabel(targetId)}", DuelCommandResult.Fail("Target must be adjacent."));
            }

            if (!CanAssignTarget(_playerLinks, sourceId, targetId, MaxConcurrentTargets))
            {
                return LogPlayerCommand("swipe-link", $"source={GetLabel(sourceId)} target={GetLabel(targetId)}", DuelCommandResult.Fail($"You can pressure at most {MaxConcurrentTargets} targets at once."));
            }

            _playerLinks[sourceId] = new DuelLinkState(sourceId, targetId, PlayerOwnerId);
            return LogPlayerCommand("swipe-link", $"source={GetLabel(sourceId)} target={GetLabel(targetId)}", DuelCommandResult.Ok($"Linked {GetLabel(sourceId)} -> {GetLabel(targetId)}."));
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

        private DuelCommandResult LogPlayerCommand(string verb, string detail, DuelCommandResult result)
        {
            var timestamp = $"{ElapsedSeconds:0.00}s";
            var activeLinks = CountActiveLinks(_playerLinks);
            _playerActionLog.Add($"[{timestamp}] {verb} {detail} | {(result.Success ? "OK" : "FAIL")} | {result.Message} | activeLinks={activeLinks}");
            return result;
        }

        private void DumpPlayerActionLog()
        {
#if UNITY_EDITOR
            var lines = new List<string>(_playerActionLog.Count + 8)
            {
                "[HexHackDuel] Player action dump",
                $"mode={AttackMode} difficulty={Difficulty} result={ResultSummary}",
                $"playerPower={PlayerPower:0.##} enemyPower={EnemyPower:0.##} elapsed={ElapsedSeconds:0.00}s"
            };

            if (_playerActionLog.Count == 0)
            {
                lines.Add("(no player actions recorded)");
            }
            else
            {
                for (var i = 0; i < _playerActionLog.Count; i++)
                {
                    lines.Add(_playerActionLog[i]);
                }
            }

            Debug.Log(string.Join("\n", lines));
#endif
        }

        private string FormatSources(List<int> sourceIds)
        {
            if (sourceIds == null || sourceIds.Count == 0) return "-";

            var labels = new string[sourceIds.Count];
            for (var i = 0; i < sourceIds.Count; i++)
            {
                labels[i] = GetLabel(sourceIds[i]);
            }

            return string.Join(",", labels);
        }

        private static int CountActiveLinks(DuelLinkState[] links)
        {
            var count = 0;
            for (var i = 0; i < links.Length; i++)
            {
                if (links[i].IsActive)
                {
                    count++;
                }
            }

            return count;
        }

        private void LogEnemyDecision(string verb, string detail)
        {
            var timestamp = $"{ElapsedSeconds:0.00}s";
            var activeLinks = CountActiveLinks(_enemyLinks);
            _enemyActionLog.Add($"[{timestamp}] {verb} {detail} | activeLinks={activeLinks}");
        }

        private void DumpEnemyActionLog()
        {
#if UNITY_EDITOR
            var lines = new List<string>(_enemyActionLog.Count + 8)
            {
                "[HexHackDuel] AI action dump",
                $"mode={AttackMode} difficulty={Difficulty} result={ResultSummary}",
                $"playerPower={PlayerPower:0.##} enemyPower={EnemyPower:0.##} elapsed={ElapsedSeconds:0.00}s"
            };

            if (_enemyActionLog.Count == 0)
            {
                lines.Add("(no AI actions recorded)");
            }
            else
            {
                for (var i = 0; i < _enemyActionLog.Count; i++)
                {
                    lines.Add(_enemyActionLog[i]);
                }
            }

            Debug.Log(string.Join("\n", lines));
#endif
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
                if (node.OwnerPlayerId != ownerPlayerId || !CanAttackFromNode(node, ownerPlayerId)) continue;
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

            AccumulatePush(_playerLinks, PlayerOwnerId, playerPush, EnemyPower <= 0d ? 1d : Math.Clamp(PlayerPower / EnemyPower, _combatConfig.EasyEnemyPowerRatio, _combatConfig.HardEnemyPowerRatio));
            AccumulatePush(_enemyLinks, EnemyOwnerId, enemyPush, PlayerPower <= 0d ? 1d : Math.Clamp(EnemyPower / PlayerPower, _combatConfig.EasyEnemyPowerRatio, _combatConfig.HardEnemyPowerRatio));

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

                var flankMultiplier = 1f + (_combatConfig.FlankBonusPerExtraAttacker * (attackers.Count - 1));
                var defenseMultiplier = GetDefenseMultiplier(targetId);

                for (var i = 0; i < attackers.Count; i++)
                {
                    var sourceId = attackers[i];
                    var push = _combatConfig.BasePushPerSecond * flankMultiplier * GetSourcePushScale(_nodes[sourceId], attackerOwnerId);

                    if (_nodes[sourceId].IsCore && _nodes[sourceId].OwnerPlayerId == attackerOwnerId)
                    {
                        push *= 1f + _combatConfig.CorePushBonus;
                    }

                    push *= (float)powerMultiplier;

                    if (ElapsedSeconds >= _combatConfig.OverdriveStartsAtSeconds)
                    {
                        push *= _combatConfig.OverdriveMultiplier;
                    }

                    output[targetId] += (push / defenseMultiplier) * GetIncomingVulnerabilityMultiplier(_nodes[targetId]);
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

            return 1f + (_combatConfig.DefenseBonusPerFriendlyNeighbor * friendlyNeighbors);
        }

        private void RebuildEnemyLinks()
        {
            if (_combatConfig.AttackMode == HexHackDuelAttackMode.ManualSwipeSources)
            {
                RebuildEnemyLinksForManualMode();
                return;
            }

            if (TrySearchBestEnemyAutoPlan(out var bestPlan, out var bestTargetId, out var selectedSources))
            {
                Array.Copy(bestPlan, _enemyLinks, _enemyLinks.Length);
                RegisterEnemyDecisionTarget(bestTargetId);
                LogEnemyDecision("auto-target", $"target={GetLabel(bestTargetId)} sources={FormatSources(selectedSources)}");
                return;
            }

            Array.Clear(_enemyLinks, 0, _enemyLinks.Length);
            RegisterEnemyDecisionTarget(-1);
            LogEnemyDecision("auto-target", "target=- sources=-");
        }

        private void RebuildEnemyLinksForManualMode()
        {
            Array.Clear(_enemyLinks, 0, _enemyLinks.Length);

            var candidates = new List<int>(_nodes.Length);
            for (var targetId = 0; targetId < _nodes.Length; targetId++)
            {
                var target = _nodes[targetId];
                if (!CanTargetNodeForPressure(target, EnemyOwnerId)) continue;
                if (CollectAutoLinkSources(targetId, EnemyOwnerId).Count == 0) continue;
                candidates.Add(targetId);
            }

            candidates.Sort((a, b) => ScoreEnemyCandidate(b).CompareTo(ScoreEnemyCandidate(a)));
            if (candidates.Count > MaxConcurrentTargets)
            {
                candidates.RemoveRange(MaxConcurrentTargets, candidates.Count - MaxConcurrentTargets);
            }

            for (var sourceId = 0; sourceId < _nodes.Length; sourceId++)
            {
                var source = _nodes[sourceId];
                if (source.OwnerPlayerId != EnemyOwnerId || !CanAttackFromNode(source, EnemyOwnerId)) continue;

                var bestTargetId = -1;
                var bestScore = float.MinValue;
                for (var i = 0; i < candidates.Count; i++)
                {
                    var targetId = candidates[i];
                    if (!IsAdjacent(sourceId, targetId)) continue;

                    var score = ScoreEnemyCandidate(targetId);
                    if (score <= bestScore) continue;
                    bestScore = score;
                    bestTargetId = targetId;
                }

                if (bestTargetId >= 0)
                {
                    _enemyLinks[sourceId] = new DuelLinkState(sourceId, bestTargetId, EnemyOwnerId);
                }
            }

            var distinctTargets = new HashSet<int>();
            var activeSources = new List<int>();
            for (var i = 0; i < _enemyLinks.Length; i++)
            {
                if (!_enemyLinks[i].IsActive) continue;
                activeSources.Add(_enemyLinks[i].SourceId);
                distinctTargets.Add(_enemyLinks[i].TargetId);
            }

            var targetLabels = new List<string>(distinctTargets.Count);
            foreach (var targetId in distinctTargets)
            {
                targetLabels.Add(GetLabel(targetId));
            }

            LogEnemyDecision(
                "manual-target",
                $"targets={(targetLabels.Count == 0 ? "-" : string.Join(",", targetLabels))} sources={FormatSources(activeSources)}");

            var singleTargetId = distinctTargets.Count == 1 ? GetOnlyTargetId(distinctTargets) : -1;
            RegisterEnemyDecisionTarget(singleTargetId);
        }

        private bool TrySearchBestEnemyAutoPlan(out DuelLinkState[] bestPlan, out int bestTargetId, out List<int> bestSources)
        {
            bestPlan = null;
            bestTargetId = -1;
            bestSources = null;

            var candidates = BuildAutoPressureCandidates(_nodes, EnemyOwnerId);
            if (candidates.Count == 0)
            {
                return false;
            }

            var bestScore = float.MinValue;
            for (var i = 0; i < candidates.Count; i++)
            {
                var candidate = candidates[i];
                var score = AdjustEnemyPlanScore(candidate.TargetId, EvaluateEnemyAutoPlan(candidate.TargetId, candidate.Sources));
                if (score <= bestScore) continue;

                bestScore = score;
                bestTargetId = candidate.TargetId;
                bestSources = candidate.Sources;
                bestPlan = candidate.Links;
            }

            return bestPlan != null;
        }

        private float AdjustEnemyPlanScore(int targetId, float baseScore)
        {
            var score = baseScore;
            var enemyCoreSources = CountOperationalNeighbors(CoreNodeId, EnemyOwnerId);
            var playerCoreSources = CountOperationalNeighbors(CoreNodeId, PlayerOwnerId);
            var enemyIncomingCore = CountIncomingLinks(_enemyLinks, CoreNodeId, EnemyOwnerId);
            var playerIncomingCore = CountIncomingLinks(_playerLinks, CoreNodeId, PlayerOwnerId);
            var enemyCoreTime = EstimateTimeToCaptureSeconds(_nodes, CoreNodeId, EnemyOwnerId);
            var playerCoreTime = EstimateTimeToCaptureSeconds(_nodes, CoreNodeId, PlayerOwnerId);
            var currentCore = _nodes[CoreNodeId];
            var coreIsContestable = currentCore.OwnerPlayerId != EnemyOwnerId || currentCore.Control > -100f;
            var losingCoreContestHeavily =
                coreIsContestable &&
                playerCoreSources >= 2 &&
                enemyCoreSources <= 1 &&
                playerIncomingCore >= enemyIncomingCore;
            var enemyWinsCoreRaceSoon =
                !float.IsInfinity(enemyCoreTime) &&
                (float.IsInfinity(playerCoreTime) || enemyCoreTime + 0.6f < playerCoreTime);

            if (targetId == CoreNodeId)
            {
                if (enemyCoreSources > playerCoreSources)
                {
                    score += 220f;
                }
                else if (enemyCoreSources == playerCoreSources && _enemyLastDecisionTargetId != CoreNodeId)
                {
                    score += 80f;
                }

                if (coreIsContestable && _enemyLastDecisionTargetId >= 0 && _enemyLastDecisionTargetId != CoreNodeId)
                {
                    score += 60f;
                }

                if (losingCoreContestHeavily)
                {
                    score -= 420f;
                }

                if (enemyWinsCoreRaceSoon)
                {
                    score += 260f;
                }
            }
            else if (targetId >= 0)
            {
                var repeatedSideTarget = _enemyLastDecisionTargetId == targetId && targetId != CoreNodeId;
                if (repeatedSideTarget && _enemySameTargetDecisionStreak >= 2)
                {
                    score -= 110f * (_enemySameTargetDecisionStreak - 1);
                }

                if (enemyCoreSources > playerCoreSources && coreIsContestable)
                {
                    score -= 180f;
                }

                if (losingCoreContestHeavily)
                {
                    score += IsAdjacent(targetId, CoreNodeId) ? 260f : 140f;
                }

                if (enemyWinsCoreRaceSoon)
                {
                    score -= 220f;
                }
            }

            return score;
        }

        private static int GetOnlyTargetId(HashSet<int> targetIds)
        {
            foreach (var targetId in targetIds)
            {
                return targetId;
            }

            return -1;
        }

        private void RegisterEnemyDecisionTarget(int targetId)
        {
            if (targetId >= 0 && _enemyLastDecisionTargetId == targetId)
            {
                _enemySameTargetDecisionStreak++;
            }
            else if (targetId >= 0)
            {
                _enemyLastDecisionTargetId = targetId;
                _enemySameTargetDecisionStreak = 1;
            }
            else
            {
                _enemyLastDecisionTargetId = -1;
                _enemySameTargetDecisionStreak = 0;
            }
        }

        private float EvaluateEnemyAutoPlan(int targetId, List<int> sources)
        {
            var simNodes = CloneNodes(_nodes);
            var simPlayerLinks = CloneLinks(_playerLinks);
            var simEnemyLinks = BuildAutoPressureLinks(sources, targetId, EnemyOwnerId);
            var horizon = GetEnemySearchHorizonSeconds();
            var replanSlice = Mathf.Min(GetEstimatedPlayerDecisionDelaySeconds(), GetProjectedEnemyDecisionDelaySeconds());
            var simulated = 0f;

            while (simulated < horizon)
            {
                var slice = Mathf.Min(replanSlice, horizon - simulated);
                SimulateState(simNodes, simPlayerLinks, simEnemyLinks, slice);
                simulated += slice;
                if (simulated >= horizon)
                {
                    break;
                }

                simEnemyLinks = FindBestProjectedEnemyAutoPlan(simNodes, simPlayerLinks);
                simPlayerLinks = FindBestProjectedPlayerAutoPlan(simNodes, simEnemyLinks);
            }

            return EvaluateBoardForEnemy(simNodes, simPlayerLinks, simEnemyLinks);
        }

        private DuelLinkState[] FindBestProjectedEnemyAutoPlan(DuelNodeState[] nodes, DuelLinkState[] playerLinks)
        {
            var candidates = BuildAutoPressureCandidates(nodes, EnemyOwnerId);
            if (candidates.Count == 0)
            {
                return new DuelLinkState[_enemyLinks.Length];
            }

            var bestScore = float.MinValue;
            DuelLinkState[] bestPlan = null;
            var responseHorizon = Mathf.Clamp(GetProjectedEnemyDecisionDelaySeconds() * 1.75f, 1.2f, 2.2f);

            for (var i = 0; i < candidates.Count; i++)
            {
                var candidate = candidates[i];
                var simNodes = CloneNodes(nodes);
                var simPlayerLinks = CloneLinks(playerLinks);
                var simEnemyLinks = CloneLinks(candidate.Links);
                SimulateState(simNodes, simPlayerLinks, simEnemyLinks, responseHorizon);
                var score = EvaluateBoardForEnemy(simNodes, simPlayerLinks, simEnemyLinks);
                if (score <= bestScore) continue;

                bestScore = score;
                bestPlan = candidate.Links;
            }

            return bestPlan ?? new DuelLinkState[_enemyLinks.Length];
        }

        private DuelLinkState[] FindBestProjectedPlayerAutoPlan(DuelNodeState[] nodes, DuelLinkState[] enemyLinks)
        {
            var candidates = BuildAutoPressureCandidates(nodes, PlayerOwnerId);
            if (candidates.Count == 0)
            {
                return new DuelLinkState[_playerLinks.Length];
            }

            var bestScore = float.MaxValue;
            DuelLinkState[] bestPlan = null;
            var responseHorizon = Mathf.Clamp(GetEstimatedPlayerDecisionDelaySeconds() * 1.5f, 0.8f, 1.6f);

            for (var i = 0; i < candidates.Count; i++)
            {
                var candidate = candidates[i];
                var simNodes = CloneNodes(nodes);
                var simPlayerLinks = CloneLinks(candidate.Links);
                var simEnemyLinks = CloneLinks(enemyLinks);
                SimulateState(simNodes, simPlayerLinks, simEnemyLinks, responseHorizon);
                var score = EvaluateBoardForEnemy(simNodes, simPlayerLinks, simEnemyLinks);
                if (score >= bestScore) continue;

                bestScore = score;
                bestPlan = candidate.Links;
            }

            return bestPlan ?? new DuelLinkState[_playerLinks.Length];
        }

        private List<AutoPressureCandidate> BuildAutoPressureCandidates(DuelNodeState[] nodes, int ownerPlayerId)
        {
            var candidates = new List<AutoPressureCandidate>(nodes.Length);
            for (var targetId = 0; targetId < nodes.Length; targetId++)
            {
                if (!CanTargetNodeForPressure(nodes[targetId], ownerPlayerId)) continue;

                var sources = CollectAutoLinkSources(nodes, targetId, ownerPlayerId);
                if (sources.Count == 0) continue;

                candidates.Add(new AutoPressureCandidate(targetId, sources, BuildAutoPressureLinks(sources, targetId, ownerPlayerId)));
            }

            return candidates;
        }

        private DuelLinkState[] BuildAutoPressureLinks(List<int> sources, int targetId, int ownerPlayerId)
        {
            var links = new DuelLinkState[_enemyLinks.Length];
            if (sources == null) return links;

            for (var i = 0; i < sources.Count; i++)
            {
                links[i] = new DuelLinkState(sources[i], targetId, ownerPlayerId);
            }

            return links;
        }

        private List<int> CollectAutoLinkSources(DuelNodeState[] nodes, int targetId, int ownerPlayerId)
        {
            var sources = new List<int>(_playerLinks.Length);
            var neighbors = Neighbors[targetId];
            for (var i = 0; i < neighbors.Length; i++)
            {
                var sourceId = neighbors[i];
                var node = nodes[sourceId];
                if (node.OwnerPlayerId != ownerPlayerId || !CanAttackFromNode(node, ownerPlayerId)) continue;
                sources.Add(sourceId);
            }

            sources.Sort((a, b) =>
            {
                var aCore = nodes[a].IsCore ? 1 : 0;
                var bCore = nodes[b].IsCore ? 1 : 0;
                var byCore = bCore.CompareTo(aCore);
                return byCore != 0 ? byCore : a.CompareTo(b);
            });

            return sources;
        }

        private float GetEnemySearchHorizonSeconds()
        {
            return 4f;
        }

        private float GetEstimatedPlayerDecisionDelaySeconds()
        {
            return _combatConfig.AttackMode == HexHackDuelAttackMode.AutoAdjacentPressure ? 0.8f : 0.6f;
        }

        private float GetProjectedEnemyDecisionDelaySeconds()
        {
            return Mathf.Clamp((_combatConfig.AiDecisionDelayMinSeconds + _combatConfig.AiDecisionDelayMaxSeconds) * 0.5f, 0.6f, 1.4f);
        }

        private DuelNodeState[] CloneNodes(DuelNodeState[] source)
        {
            var clone = new DuelNodeState[source.Length];
            Array.Copy(source, clone, source.Length);
            return clone;
        }

        private DuelLinkState[] CloneLinks(DuelLinkState[] source)
        {
            var clone = new DuelLinkState[source.Length];
            Array.Copy(source, clone, source.Length);
            return clone;
        }

        private void SimulateState(DuelNodeState[] nodes, DuelLinkState[] playerLinks, DuelLinkState[] enemyLinks, float durationSeconds)
        {
            var remaining = durationSeconds;
            while (remaining >= _combatConfig.TickIntervalSeconds)
            {
                remaining -= _combatConfig.TickIntervalSeconds;
                SimulateTickState(nodes, playerLinks, enemyLinks, _combatConfig.TickIntervalSeconds);
            }
        }

        private void SimulateTickState(DuelNodeState[] nodes, DuelLinkState[] playerLinks, DuelLinkState[] enemyLinks, float dt)
        {
            PruneInvalidLinksState(playerLinks, nodes, PlayerOwnerId);
            PruneInvalidLinksState(enemyLinks, nodes, EnemyOwnerId);

            var playerPush = new float[nodes.Length];
            var enemyPush = new float[nodes.Length];

            AccumulatePushState(nodes, playerLinks, PlayerOwnerId, playerPush, EnemyPower <= 0d ? 1d : Math.Clamp(PlayerPower / EnemyPower, _combatConfig.EasyEnemyPowerRatio, _combatConfig.HardEnemyPowerRatio));
            AccumulatePushState(nodes, enemyLinks, EnemyOwnerId, enemyPush, PlayerPower <= 0d ? 1d : Math.Clamp(EnemyPower / PlayerPower, _combatConfig.EasyEnemyPowerRatio, _combatConfig.HardEnemyPowerRatio));

            for (var i = 0; i < nodes.Length; i++)
            {
                var node = nodes[i];
                var next = node.Control + ((playerPush[i] - enemyPush[i]) * dt);
                nodes[i] = node.WithState(Math.Clamp(next, -100f, 100f), node.OwnerPlayerId);
            }

            ResolveCapturesState(nodes, playerLinks, enemyLinks);
        }

        private void AccumulatePushState(DuelNodeState[] nodes, DuelLinkState[] links, int attackerOwnerId, float[] output, double powerMultiplier)
        {
            for (var targetId = 0; targetId < nodes.Length; targetId++)
            {
                var attackers = CollectAttackersState(nodes, links, targetId, attackerOwnerId);
                if (attackers.Count == 0) continue;

                var flankMultiplier = 1f + (_combatConfig.FlankBonusPerExtraAttacker * (attackers.Count - 1));
                var defenseMultiplier = GetDefenseMultiplierState(nodes, targetId);

                for (var i = 0; i < attackers.Count; i++)
                {
                    var sourceId = attackers[i];
                    var push = _combatConfig.BasePushPerSecond * flankMultiplier * GetSourcePushScale(nodes[sourceId], attackerOwnerId);

                    if (nodes[sourceId].IsCore && nodes[sourceId].OwnerPlayerId == attackerOwnerId)
                    {
                        push *= 1f + _combatConfig.CorePushBonus;
                    }

                    push *= (float)powerMultiplier;
                    push *= GetProjectedOverdriveMultiplier();
                    output[targetId] += (push / defenseMultiplier) * GetIncomingVulnerabilityMultiplier(nodes[targetId]);
                }
            }
        }

        private float GetProjectedOverdriveMultiplier()
        {
            return ElapsedSeconds >= _combatConfig.OverdriveStartsAtSeconds
                ? _combatConfig.OverdriveMultiplier
                : 1f;
        }

        private List<int> CollectAttackersState(DuelNodeState[] nodes, DuelLinkState[] links, int targetId, int attackerOwnerId)
        {
            var result = new List<int>(_playerLinks.Length);
            for (var i = 0; i < links.Length; i++)
            {
                var link = links[i];
                if (!link.IsActive || link.TargetId != targetId) continue;
                if (nodes[link.SourceId].OwnerPlayerId != attackerOwnerId) continue;
                result.Add(link.SourceId);
            }

            return result;
        }

        private float GetDefenseMultiplierState(DuelNodeState[] nodes, int targetId)
        {
            var owner = nodes[targetId].OwnerPlayerId;
            if (owner == NeutralOwnerId) return 1f;

            var friendlyNeighbors = 0;
            var neighbors = Neighbors[targetId];
            for (var i = 0; i < neighbors.Length; i++)
            {
                if (nodes[neighbors[i]].OwnerPlayerId == owner)
                {
                    friendlyNeighbors++;
                }
            }

            return 1f + (_combatConfig.DefenseBonusPerFriendlyNeighbor * friendlyNeighbors);
        }

        private void PruneInvalidLinksState(DuelLinkState[] links, DuelNodeState[] nodes, int ownerPlayerId)
        {
            for (var i = 0; i < links.Length; i++)
            {
                var link = links[i];
                if (!link.IsActive) continue;

                if (nodes[link.SourceId].OwnerPlayerId != ownerPlayerId || !CanAttackFromNode(nodes[link.SourceId], ownerPlayerId))
                {
                    links[i] = default;
                    continue;
                }

                if (!CanTargetNodeForPressure(nodes[link.TargetId], ownerPlayerId) || !IsAdjacent(link.SourceId, link.TargetId))
                {
                    links[i] = default;
                    continue;
                }

                if (!CanAssignTarget(links, link.SourceId, link.TargetId, MaxConcurrentTargets))
                {
                    links[i] = default;
                }
            }
        }

        private void ResolveCapturesState(DuelNodeState[] nodes, DuelLinkState[] playerLinks, DuelLinkState[] enemyLinks)
        {
            for (var i = 0; i < nodes.Length; i++)
            {
                var node = nodes[i];
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

                nodes[i] = node.WithState(nextControl, nextOwner);

                if (previousOwner != nextOwner)
                {
                    RemoveLinksFromSourceState(playerLinks, node.Id);
                    RemoveLinksFromSourceState(enemyLinks, node.Id);
                }
            }
        }

        private static int RemoveLinksFromSourceState(DuelLinkState[] links, int sourceId)
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

        private float EvaluateBoardForEnemy(DuelNodeState[] nodes, DuelLinkState[] playerLinks, DuelLinkState[] enemyLinks)
        {
            var score = 0f;
            var playerOperational = 0;
            var enemyOperational = 0;
            var core = nodes[CoreNodeId];

            for (var i = 0; i < nodes.Length; i++)
            {
                var node = nodes[i];
                if (node.OwnerPlayerId == EnemyOwnerId)
                {
                    score += 140f;
                    score += Mathf.Clamp(-node.Control, 0f, 100f) * 0.7f;
                    if (CanAttackFromNode(node, EnemyOwnerId))
                    {
                        enemyOperational++;
                    }
                }
                else if (node.OwnerPlayerId == PlayerOwnerId)
                {
                    score -= 140f;
                    score -= Mathf.Clamp(node.Control, 0f, 100f) * 0.7f;
                    if (CanAttackFromNode(node, PlayerOwnerId))
                    {
                        playerOperational++;
                    }
                }

                if (i == CoreNodeId)
                {
                    score += node.OwnerPlayerId switch
                    {
                        EnemyOwnerId => 360f,
                        PlayerOwnerId => -360f,
                        _ => -node.Control * 3.2f
                    };
                }
            }

            score += enemyOperational * 95f;
            score -= playerOperational * 95f;
            score += CountActiveLinks(enemyLinks) * 12f;
            score -= CountActiveLinks(playerLinks) * 12f;
            score += GetProjectedDisruptionValue(nodes, playerLinks);
            score += GetProjectedCoreSourceAdvantageValue(nodes, playerLinks, enemyLinks);
            score += GetProjectedCoreRaceTimeValue(nodes);

            if (core.OwnerPlayerId == NeutralOwnerId)
            {
                score += GetProjectedCoreDeadlockBreakValue(nodes, playerLinks, enemyLinks);
            }

            return score;
        }

        private float GetProjectedDisruptionValue(DuelNodeState[] nodes, DuelLinkState[] playerLinks)
        {
            var value = 0f;
            for (var i = 0; i < nodes.Length; i++)
            {
                var node = nodes[i];
                if (node.OwnerPlayerId != PlayerOwnerId) continue;
                if (CanAttackFromNode(node, PlayerOwnerId)) continue;

                value += 45f;
                if (HasActiveLinkFromSource(playerLinks, i, PlayerOwnerId))
                {
                    value += 80f;
                }
            }

            return value;
        }

        private float GetProjectedCoreSourceAdvantageValue(DuelNodeState[] nodes, DuelLinkState[] playerLinks, DuelLinkState[] enemyLinks)
        {
            var enemyCoreSources = CountOperationalNeighbors(nodes, CoreNodeId, EnemyOwnerId);
            var playerCoreSources = CountOperationalNeighbors(nodes, CoreNodeId, PlayerOwnerId);
            var enemyIncomingCore = CountIncomingLinks(enemyLinks, CoreNodeId, EnemyOwnerId);
            var playerIncomingCore = CountIncomingLinks(playerLinks, CoreNodeId, PlayerOwnerId);

            var sourceAdvantage = enemyCoreSources - playerCoreSources;
            var incomingAdvantage = enemyIncomingCore - playerIncomingCore;
            var value = (sourceAdvantage * 180f) + (incomingAdvantage * 85f);

            if (enemyCoreSources >= 2 && playerCoreSources <= 1)
            {
                value += 220f;
            }

            if (playerCoreSources >= 2 && enemyCoreSources <= 1)
            {
                value -= 220f;
            }

            return value;
        }

        private float GetProjectedCoreDeadlockBreakValue(DuelNodeState[] nodes, DuelLinkState[] playerLinks, DuelLinkState[] enemyLinks)
        {
            var core = nodes[CoreNodeId];
            var enemyCoreSources = CountOperationalNeighbors(nodes, CoreNodeId, EnemyOwnerId);
            var playerCoreSources = CountOperationalNeighbors(nodes, CoreNodeId, PlayerOwnerId);
            var enemyIncomingCore = CountIncomingLinks(enemyLinks, CoreNodeId, EnemyOwnerId);
            var playerIncomingCore = CountIncomingLinks(playerLinks, CoreNodeId, PlayerOwnerId);
            var controlMagnitude = Mathf.Abs(core.Control);

            var value = 0f;
            var isDeadlock = controlMagnitude < 30f && enemyIncomingCore == playerIncomingCore;
            if (!isDeadlock)
            {
                return value;
            }

            if (enemyCoreSources <= playerCoreSources)
            {
                value -= 180f;
            }

            if (enemyCoreSources > playerCoreSources)
            {
                value += 180f;
            }

            if (enemyCoreSources == playerCoreSources)
            {
                value -= 90f;
            }

            return value;
        }

        private float GetProjectedCoreRaceTimeValue(DuelNodeState[] nodes)
        {
            var enemyTime = EstimateTimeToCaptureSeconds(nodes, CoreNodeId, EnemyOwnerId);
            var playerTime = EstimateTimeToCaptureSeconds(nodes, CoreNodeId, PlayerOwnerId);

            if (float.IsInfinity(enemyTime) && float.IsInfinity(playerTime))
            {
                return 0f;
            }

            if (float.IsInfinity(enemyTime))
            {
                return -260f;
            }

            if (float.IsInfinity(playerTime))
            {
                return 260f;
            }

            var delta = playerTime - enemyTime;
            return Mathf.Clamp(delta * 95f, -320f, 320f);
        }

        private float ScoreEnemyCandidate(int targetId)
        {
            var node = _nodes[targetId];
            var score = (float)_seedRng.NextDouble();
            var coreRaceLosing = IsCoreRaceLosingForEnemy();
            var incomingPlayerLinks = CountIncomingLinks(_playerLinks, targetId, PlayerOwnerId);
            var enemyAdjacentCount = CountOperationalNeighbors(targetId, EnemyOwnerId);

            if (node.IsCore)
            {
                score += node.OwnerPlayerId switch
                {
                    EnemyOwnerId => 240f,
                    PlayerOwnerId => 320f,
                    _ => 280f
                };
                score += incomingPlayerLinks * 120f;
                score += Mathf.Clamp(node.Control, -100f, 100f) * 2.1f;
            }

            if (node.OwnerPlayerId == NeutralOwnerId)
            {
                score += 40f;
                if (!node.IsCore && IsAdjacent(targetId, CoreNodeId))
                {
                    score += coreRaceLosing ? 340f : 110f;
                }
            }

            if (node.OwnerPlayerId == PlayerOwnerId)
            {
                score += 120f;
                score += Mathf.Clamp(node.Control, 0f, 100f) * 1.4f;
                score += GetPlayerSourceDisruptionScore(targetId);

                if (incomingPlayerLinks == 0 && !node.IsCore)
                {
                    score += 40f;
                }
            }
            else if (node.OwnerPlayerId == EnemyOwnerId)
            {
                var damage = Mathf.Clamp(node.Control + 100f, 0f, 100f);
                score += damage * 2.2f;
                score += incomingPlayerLinks * 140f;
                if (node.IsCore)
                {
                    score += 180f;
                }
            }

            if (enemyAdjacentCount >= 2)
            {
                score += 120f;
            }

            score += GetCoreExpansionScore(targetId, coreRaceLosing);

            return score;
        }

        private bool IsCoreRaceLosingForEnemy()
        {
            var core = _nodes[CoreNodeId];
            var playerCoreSources = CountOperationalNeighbors(CoreNodeId, PlayerOwnerId);
            var enemyCoreSources = CountOperationalNeighbors(CoreNodeId, EnemyOwnerId);
            var incomingPlayerLinks = CountIncomingLinks(_playerLinks, CoreNodeId, PlayerOwnerId);
            var incomingEnemyLinks = CountIncomingLinks(_enemyLinks, CoreNodeId, EnemyOwnerId);

            if (core.OwnerPlayerId == PlayerOwnerId && core.Control > 0f)
            {
                return true;
            }

            if (incomingPlayerLinks > incomingEnemyLinks)
            {
                return true;
            }

            return playerCoreSources > enemyCoreSources;
        }

        private float GetPlayerSourceDisruptionScore(int targetId)
        {
            var node = _nodes[targetId];
            if (node.OwnerPlayerId != PlayerOwnerId)
            {
                return 0f;
            }

            var score = 0f;
            if (HasActiveLinkFromSource(_playerLinks, targetId, PlayerOwnerId))
            {
                score += 240f;
            }

            if (CanAttackFromNode(node, PlayerOwnerId))
            {
                score += 90f;
            }

            if (IsAdjacent(targetId, CoreNodeId))
            {
                score += 170f;
            }

            var frontierCount = 0;
            var neighbors = Neighbors[targetId];
            for (var i = 0; i < neighbors.Length; i++)
            {
                if (_nodes[neighbors[i]].OwnerPlayerId != PlayerOwnerId)
                {
                    frontierCount++;
                }
            }

            score += frontierCount * 28f;
            return score;
        }

        private float GetCoreExpansionScore(int targetId, bool coreRaceLosing)
        {
            if (targetId == CoreNodeId || !IsAdjacent(targetId, CoreNodeId))
            {
                return 0f;
            }

            var node = _nodes[targetId];
            if (node.OwnerPlayerId == EnemyOwnerId)
            {
                return 0f;
            }

            if (node.OwnerPlayerId == NeutralOwnerId)
            {
                return coreRaceLosing ? 120f : 45f;
            }

            return coreRaceLosing ? 90f : 35f;
        }

        private static bool HasActiveLinkFromSource(DuelLinkState[] links, int sourceId, int ownerPlayerId)
        {
            if (sourceId < 0 || sourceId >= links.Length)
            {
                return false;
            }

            var link = links[sourceId];
            return link.IsActive && link.SourceId == sourceId && link.OwnerPlayerId == ownerPlayerId;
        }

        private int CountIncomingLinks(DuelLinkState[] links, int targetId, int ownerPlayerId)
        {
            var count = 0;
            for (var i = 0; i < links.Length; i++)
            {
                if (!links[i].IsActive) continue;
                if (links[i].OwnerPlayerId != ownerPlayerId) continue;
                if (links[i].TargetId == targetId)
                {
                    count++;
                }
            }

            return count;
        }

        private int CountOperationalNeighbors(int targetId, int ownerPlayerId)
        {
            return CountOperationalNeighbors(_nodes, targetId, ownerPlayerId);
        }

        private int CountOperationalNeighbors(DuelNodeState[] nodes, int targetId, int ownerPlayerId)
        {
            var count = 0;
            var neighbors = Neighbors[targetId];
            for (var i = 0; i < neighbors.Length; i++)
            {
                var node = nodes[neighbors[i]];
                if (node.OwnerPlayerId != ownerPlayerId) continue;
                if (CanAttackFromNode(node, ownerPlayerId))
                {
                    count++;
                }
            }

            return count;
        }

        private float EstimateTimeToCaptureSeconds(DuelNodeState[] nodes, int targetId, int attackerOwnerId)
        {
            var target = nodes[targetId];
            var attackerPush = ComputePotentialPushPerSecond(nodes, targetId, attackerOwnerId);
            var defenderOwnerId = attackerOwnerId == PlayerOwnerId ? EnemyOwnerId : PlayerOwnerId;
            var defenderPush = ComputePotentialPushPerSecond(nodes, targetId, defenderOwnerId);
            var netPush = attackerPush - defenderPush;
            if (netPush <= 0.01f)
            {
                return float.PositiveInfinity;
            }

            var remainingControl = attackerOwnerId == PlayerOwnerId
                ? 100f - target.Control
                : target.Control + 100f;

            remainingControl = Mathf.Max(0f, remainingControl);
            return remainingControl / netPush;
        }

        private float ComputePotentialPushPerSecond(DuelNodeState[] nodes, int targetId, int attackerOwnerId)
        {
            var sources = CollectAutoLinkSources(nodes, targetId, attackerOwnerId);
            if (sources.Count == 0)
            {
                return 0f;
            }

            var flankMultiplier = 1f + (_combatConfig.FlankBonusPerExtraAttacker * (sources.Count - 1));
            var defenseMultiplier = GetDefenseMultiplierState(nodes, targetId);
            var powerMultiplier = attackerOwnerId == PlayerOwnerId
                ? (EnemyPower <= 0d ? 1d : Math.Clamp(PlayerPower / EnemyPower, _combatConfig.EasyEnemyPowerRatio, _combatConfig.HardEnemyPowerRatio))
                : (PlayerPower <= 0d ? 1d : Math.Clamp(EnemyPower / PlayerPower, _combatConfig.EasyEnemyPowerRatio, _combatConfig.HardEnemyPowerRatio));

            var total = 0f;
            for (var i = 0; i < sources.Count; i++)
            {
                var sourceId = sources[i];
                var push = _combatConfig.BasePushPerSecond * flankMultiplier * GetSourcePushScale(nodes[sourceId], attackerOwnerId);
                if (nodes[sourceId].IsCore && nodes[sourceId].OwnerPlayerId == attackerOwnerId)
                {
                    push *= 1f + _combatConfig.CorePushBonus;
                }

                push *= (float)powerMultiplier;
                total += (push / defenseMultiplier) * GetIncomingVulnerabilityMultiplier(nodes[targetId]);
            }

            return total;
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

                if (_nodes[link.SourceId].OwnerPlayerId != ownerPlayerId || !CanAttackFromNode(_nodes[link.SourceId], ownerPlayerId))
                {
                    links[i] = default;
                    continue;
                }

                if (!CanTargetNodeForPressure(_nodes[link.TargetId], ownerPlayerId) || !IsAdjacent(link.SourceId, link.TargetId))
                {
                    links[i] = default;
                    continue;
                }

                if (!CanAssignTarget(links, link.SourceId, link.TargetId, MaxConcurrentTargets))
                {
                    links[i] = default;
                }
            }
        }

        private static bool CanAssignTarget(DuelLinkState[] links, int sourceId, int targetId, int maxConcurrentTargets)
        {
            var uniqueTargets = new HashSet<int>();
            for (var i = 0; i < links.Length; i++)
            {
                var link = links[i];
                if (!link.IsActive || link.SourceId == sourceId) continue;
                uniqueTargets.Add(link.TargetId);
            }

            if (uniqueTargets.Contains(targetId))
            {
                return true;
            }

            return uniqueTargets.Count < maxConcurrentTargets;
        }

        private static bool CanTargetNodeForPressure(DuelNodeState node, int ownerPlayerId)
        {
            if (node.OwnerPlayerId != ownerPlayerId)
            {
                return true;
            }

            return !IsFullyControlledByOwner(node, ownerPlayerId);
        }

        private bool CanAttackFromNode(DuelNodeState node, int ownerPlayerId)
        {
            if (node.OwnerPlayerId != ownerPlayerId)
            {
                return false;
            }

            var threshold = _combatConfig.AttackControlThreshold;
            return ownerPlayerId switch
            {
                PlayerOwnerId => node.Control > threshold,
                EnemyOwnerId => node.Control < -threshold,
                _ => false
            };
        }

        private float GetSourcePushScale(DuelNodeState node, int ownerPlayerId)
        {
            var signedControl = ownerPlayerId == PlayerOwnerId ? node.Control : -node.Control;
            var normalizedControl = Mathf.Clamp01(signedControl / 100f);
            return _combatConfig.OperationalMinPushScale + ((1f - _combatConfig.OperationalMinPushScale) * normalizedControl);
        }

        private float GetIncomingVulnerabilityMultiplier(DuelNodeState node)
        {
            if (node.OwnerPlayerId == NeutralOwnerId)
            {
                return 1f;
            }

            var ownerControl = node.OwnerPlayerId == PlayerOwnerId ? node.Control : -node.Control;
            return ownerControl < _combatConfig.LowControlVulnerabilityThreshold
                ? _combatConfig.LowControlVulnerabilityMultiplier
                : 1f;
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

            var playerCount = PlayerHexCount;
            var enemyCount = EnemyHexCount;
            if (playerCount > enemyCount)
            {
                ResultSummary = $"Victory {playerCount}:{enemyCount}";
            }
            else if (enemyCount > playerCount)
            {
                ResultSummary = $"Defeat {playerCount}:{enemyCount}";
            }
            else
            {
                var coreOwner = _nodes[CoreNodeId].OwnerPlayerId;
                ResultSummary = coreOwner switch
                {
                    PlayerOwnerId => $"Victory by CORE {playerCount}:{enemyCount}",
                    EnemyOwnerId => $"Defeat by CORE {playerCount}:{enemyCount}",
                    _ => $"Draw {playerCount}:{enemyCount}"
                };
            }

            DumpPlayerActionLog();
            DumpEnemyActionLog();
            Array.Clear(_playerLinks, 0, _playerLinks.Length);
            Array.Clear(_enemyLinks, 0, _enemyLinks.Length);
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
            _playerActionLog.Clear();
            _enemyActionLog.Clear();
            _enemyLastDecisionTargetId = -1;
            _enemySameTargetDecisionStreak = 0;
            PlayerPower = 100d;
            EnemyPower = 100d;
            _remainingSeconds = MatchDurationSeconds;
            IsMatchActive = false;
            IsMatchFinished = false;
            ResultSummary = "Press PLAY to start a duel.";
        }

        private float NextDecisionDelay()
        {
            var min = _combatConfig.AiDecisionDelayMinSeconds;
            var max = _combatConfig.AiDecisionDelayMaxSeconds;
            return min + ((float)_seedRng.NextDouble() * (max - min));
        }

        private float ResolveInitialDecisionDelay()
        {
            return _combatConfig.InitialAiDecisionDelaySeconds > 0f
                ? _combatConfig.InitialAiDecisionDelaySeconds
                : NextDecisionDelay();
        }

        private double ResolveEnemyPower(double playerPower, DuelDifficulty difficulty)
        {
            var safePlayerPower = Math.Max(1d, playerPower);
            return difficulty switch
            {
                DuelDifficulty.Easy => safePlayerPower * _combatConfig.EasyEnemyPowerRatio,
                DuelDifficulty.Hard => safePlayerPower * _combatConfig.HardEnemyPowerRatio,
                _ => safePlayerPower * ResolveMediumEnemyPowerRatio()
            };
        }

        private double ResolveMediumEnemyPowerRatio()
        {
            var easy = _combatConfig.EasyEnemyPowerRatio;
            var hard = _combatConfig.HardEnemyPowerRatio;
            var min = 1d - ((1d - easy) * 0.5d);
            var max = 1d + ((hard - 1d) * 0.5d);
            return min + (_seedRng.NextDouble() * (max - min));
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

        private bool HasFullBoardControl(int ownerPlayerId)
        {
            return CountOwned(ownerPlayerId) == _nodes.Length;
        }

        private bool HasOperationalHex(int ownerPlayerId)
        {
            for (var i = 0; i < _nodes.Length; i++)
            {
                if (CanAttackFromNode(_nodes[i], ownerPlayerId))
                {
                    return true;
                }
            }

            return false;
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

        private readonly struct AutoPressureCandidate
        {
            public readonly int TargetId;
            public readonly List<int> Sources;
            public readonly DuelLinkState[] Links;

            public AutoPressureCandidate(int targetId, List<int> sources, DuelLinkState[] links)
            {
                TargetId = targetId;
                Sources = sources;
                Links = links;
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
