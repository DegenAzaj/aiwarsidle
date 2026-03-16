using AIWarsIdle.PvP.Config;
using AIWarsIdle.PvP.Services;
using NUnit.Framework;
using System.Reflection;
using UnityEngine;

namespace AIWarsIdle.Tests
{
    public sealed class HexHackDuelServiceTests
    {
        [Test]
        public void StartMatch_ResetsBoardToTwoThreeTwoRingSetup()
        {
            var duel = new HexHackDuelService();

            duel.StartMatch();

            Assert.IsTrue(duel.IsMatchActive);
            Assert.AreEqual(30f, duel.RemainingSeconds, 0.001f);
            Assert.AreEqual(7, duel.Nodes.Count);
            Assert.AreEqual(0, duel.Nodes[0].OwnerPlayerId);
            Assert.AreEqual(1, duel.Nodes[2].OwnerPlayerId);
            Assert.AreEqual(2, duel.Nodes[4].OwnerPlayerId);
            Assert.AreEqual(0f, duel.Nodes[3].Control, 0.001f);
        }

        [Test]
        public void StartMatch_GivesEachSideSingleStartingHex()
        {
            var duel = new HexHackDuelService();

            duel.StartMatch();

            Assert.AreEqual(0, duel.Nodes[1].OwnerPlayerId);
            Assert.AreEqual(1, duel.PlayerHexCount);
            Assert.AreEqual(1, duel.EnemyHexCount);
        }

        [Test]
        public void StartMatch_MediumDifficultyRollsEnemyPowerWithinMiddleHalfOfClampRange()
        {
            var duel = new HexHackDuelService(seed: 7);

            duel.StartMatch();

            Assert.AreEqual(DuelDifficulty.Medium, duel.Difficulty);
            Assert.GreaterOrEqual(duel.EnemyPower, duel.PlayerPower * 0.85d);
            Assert.LessOrEqual(duel.EnemyPower, duel.PlayerPower * 1.15d);
            Assert.Greater(System.Math.Abs(duel.PlayerPower - duel.EnemyPower), 0.0001d);
        }

        [Test]
        public void CycleDifficulty_ChangesEnemyPowerClampOnStart()
        {
            var duel = new HexHackDuelService();

            duel.CycleDifficulty();
            duel.StartMatch();
            Assert.AreEqual(DuelDifficulty.Hard, duel.Difficulty);
            Assert.AreEqual(duel.PlayerPower * 1.3d, duel.EnemyPower, 0.001d);

            duel.CycleDifficulty();
            duel.StartMatch();
            Assert.AreEqual(DuelDifficulty.Easy, duel.Difficulty);
            Assert.AreEqual(duel.PlayerPower * 0.7d, duel.EnemyPower, 0.001d);
        }

        [Test]
        public void StartMatch_UsesConfiguredInitialAiDecisionDelayBeforeFirstAction()
        {
            var config = CreateSlowAiConfig();
            config.HexHackDuelCombat.InitialAiDecisionDelaySeconds = 0.5f;
            config.HexHackDuelCombat.AiDecisionDelayMinSeconds = 99f;
            config.HexHackDuelCombat.AiDecisionDelayMaxSeconds = 99f;
            var duel = new HexHackDuelService(pvpConfig: config, seed: 7);

            duel.StartMatch();
            duel.Tick(0.49f);
            Assert.IsFalse(HasAnyActiveLink(duel.EnemyLinks));

            duel.Tick(0.02f);
            Assert.IsTrue(HasAnyActiveLink(duel.EnemyLinks));
        }

        [Test]
        public void PlayerLink_PushesNeutralNodeForwardDuringTicks()
        {
            var duel = new HexHackDuelService(seed: 7);
            duel.StartMatch();

            var result = duel.AutoAssignPlayerLinksToTarget(0);
            Assert.IsTrue(result.Success);

            duel.Tick(1.0f);

            Assert.Greater(duel.Nodes[0].Control, 20f);
            Assert.Less(duel.Nodes[0].Control, 30f);
        }

        [Test]
        public void DamagedOwnedNodeAboveThreshold_CanStillAttackWithReducedPush()
        {
            var duel = new HexHackDuelService(seed: 7);
            duel.StartMatch();

            Assert.IsTrue(duel.AutoAssignPlayerLinksToTarget(0).Success);
            duel.Tick(3.0f);
            duel.ClearAllPlayerLinks();

            Assert.IsTrue(duel.AutoAssignPlayerLinksToTarget(3).Success);
            duel.Tick(0.5f);

            Assert.Greater(duel.Nodes[3].Control, 0f);
            Assert.Less(duel.Nodes[3].Control, 15f);
        }

        [Test]
        public void DamagedOwnedNodeAtOrBelowThreshold_CannotAttack()
        {
            var duel = new HexHackDuelService(seed: 7);
            duel.StartMatch();

            Assert.IsTrue(duel.AutoAssignPlayerLinksToTarget(3).Success);
            duel.Tick(3.2f);

            var result = duel.TryAssignPlayerLinkFromSource(3, 1);

            Assert.IsFalse(result.Success);
        }

        [Test]
        public void AdjacentAndNonAdjacentChecksMatchTwoThreeTwoLayout()
        {
            var duel = new HexHackDuelService(seed: 7);
            duel.StartMatch();

            Assert.IsTrue(duel.IsAdjacent(2, 0));
            Assert.IsTrue(duel.IsAdjacent(2, 3));
            Assert.IsFalse(duel.IsAdjacent(2, 4));
        }

        [Test]
        public void AutoAssignPlayerLinksToTarget_UsesAdjacentOwnedSources()
        {
            var duel = new HexHackDuelService(seed: 7);
            duel.StartMatch();

            var result = duel.AutoAssignPlayerLinksToTarget(0);

            Assert.IsTrue(result.Success);
            Assert.AreEqual(2, duel.PlayerLinks[0].SourceId);
            Assert.AreEqual(0, duel.PlayerLinks[0].TargetId);
            Assert.IsFalse(duel.PlayerLinks[1].IsActive);
        }

        [Test]
        public void AutoAssignPlayerLinksToTarget_AllowsDefendingDamagedOwnedNode()
        {
            var duel = new HexHackDuelService(seed: 7);
            duel.StartMatch();

            var attackStart = duel.AutoAssignPlayerLinksToTarget(3);
            Assert.IsTrue(attackStart.Success);

            duel.Tick(4.1f);
            Assert.AreEqual(1, duel.Nodes[3].OwnerPlayerId);
            Assert.Less(duel.Nodes[3].Control, 100f);

            var defend = duel.AutoAssignPlayerLinksToTarget(3);

            Assert.IsTrue(defend.Success);
            Assert.AreEqual(2, duel.PlayerLinks[0].SourceId);
            Assert.AreEqual(3, duel.PlayerLinks[0].TargetId);
        }

        [Test]
        public void EnemyAi_CreatesLinkAfterFirstDecisionWindow()
        {
            var duel = new HexHackDuelService(seed: 7);
            duel.StartMatch();

            duel.Tick(2.6f);

            Assert.IsTrue(duel.EnemyLinks[0].IsActive);
            Assert.AreEqual(4, duel.EnemyLinks[0].SourceId);
        }

        [Test]
        public void EnemyAi_AppliesPressureAfterDecision()
        {
            var duel = new HexHackDuelService(seed: 7);
            duel.StartMatch();

            duel.Tick(2.6f);

            var before = duel.Nodes[3].Control;
            duel.Tick(0.5f);
            var after = duel.Nodes[3].Control;

            Assert.Less(after, before);
        }

        [Test]
        public void EnemyAi_FocusesSingleTargetPerDecision()
        {
            var duel = new HexHackDuelService(seed: 7);
            duel.StartMatch();

            duel.Tick(2.6f);

            var targetId = duel.EnemyLinks[0].TargetId;
            for (var i = 1; i < duel.EnemyLinks.Count; i++)
            {
                if (!duel.EnemyLinks[i].IsActive) continue;
                Assert.AreEqual(targetId, duel.EnemyLinks[i].TargetId);
            }
        }

        [Test]
        public void EnemyAi_FlanksWhenCoreRaceIsLosing()
        {
            var duel = new HexHackDuelService(pvpConfig: CreateSlowAiConfig(), seed: 7);
            duel.StartMatch();

            Assert.IsTrue(duel.AutoAssignPlayerLinksToTarget(0).Success);
            duel.Tick(4.1f);
            Assert.AreEqual(1, duel.Nodes[0].OwnerPlayerId);

            Assert.IsTrue(duel.AutoAssignPlayerLinksToTarget(3).Success);

            var rebuild = typeof(HexHackDuelService).GetMethod("RebuildEnemyLinks", BindingFlags.Instance | BindingFlags.NonPublic);
            rebuild.Invoke(duel, null);

            Assert.IsTrue(duel.EnemyLinks[0].IsActive);
            Assert.Contains(duel.EnemyLinks[0].TargetId, new[] { 1, 6 });
        }

        [Test]
        public void EnemyAi_DoesNotKeepHardLosingOneSourceCoreContest()
        {
            var duel = new HexHackDuelService(pvpConfig: CreateSlowAiConfig(), seed: 7);
            duel.StartMatch();

            Assert.IsTrue(duel.AutoAssignPlayerLinksToTarget(0).Success);
            duel.Tick(4.1f);
            Assert.AreEqual(1, duel.Nodes[0].OwnerPlayerId);

            Assert.IsTrue(duel.AutoAssignPlayerLinksToTarget(3).Success);

            var rebuild = typeof(HexHackDuelService).GetMethod("RebuildEnemyLinks", BindingFlags.Instance | BindingFlags.NonPublic);
            rebuild.Invoke(duel, null);

            Assert.IsTrue(duel.EnemyLinks[0].IsActive);
            Assert.AreNotEqual(3, duel.EnemyLinks[0].TargetId);
        }

        [Test]
        public void Match_EndsImmediatelyWhenOneSideOwnsAllNodes()
        {
            var duel = new HexHackDuelService(seed: 7);
            duel.StartMatch();

            Assert.IsTrue(duel.AutoAssignPlayerLinksToTarget(0).Success);
            duel.Tick(4.1f);
            Assert.IsTrue(duel.AutoAssignPlayerLinksToTarget(3).Success);
            duel.Tick(4.1f);
            Assert.IsTrue(duel.AutoAssignPlayerLinksToTarget(1).Success);
            duel.Tick(4.1f);
            Assert.IsTrue(duel.AutoAssignPlayerLinksToTarget(5).Success);
            duel.Tick(4.1f);
            Assert.IsTrue(duel.AutoAssignPlayerLinksToTarget(6).Success);
            duel.Tick(4.1f);
            Assert.IsTrue(duel.AutoAssignPlayerLinksToTarget(4).Success);
            duel.Tick(4.1f);

            Assert.IsFalse(duel.IsMatchActive);
            Assert.IsTrue(duel.IsMatchFinished);
            Assert.AreEqual(7, duel.PlayerHexCount);
            Assert.Less(duel.RemainingSeconds, 30f);
        }

        [Test]
        public void Match_EndsImmediatelyWhenSideLosesAllOperationalHexes()
        {
            var duel = new HexHackDuelService(pvpConfig: CreateSlowAiConfig(), seed: 7);
            duel.StartMatch();

            Assert.IsTrue(duel.AutoAssignPlayerLinksToTarget(3).Success);
            duel.Tick(4.1f);
            Assert.AreEqual(1, duel.Nodes[3].OwnerPlayerId);

            Assert.IsTrue(duel.AutoAssignPlayerLinksToTarget(4).Success);
            duel.Tick(3.0f);

            Assert.IsFalse(duel.IsMatchActive);
            Assert.IsTrue(duel.IsMatchFinished);
            Assert.AreEqual(2, duel.Nodes[4].OwnerPlayerId);
            Assert.Greater(duel.Nodes[4].Control, -10f);
            Assert.Less(duel.RemainingSeconds, 30f);
        }

        [Test]
        public void ManualSwipeMode_PlayerCanAssignAndCancelSourceLink()
        {
            var duel = new HexHackDuelService(pvpConfig: CreateManualSwipeConfig(), seed: 7);
            duel.StartMatch();

            var assign = duel.TryAssignPlayerLinkFromSource(2, 3);

            Assert.IsTrue(assign.Success);
            Assert.IsTrue(duel.HasPlayerLinkFromSource(2));
            Assert.AreEqual(3, duel.PlayerLinks[2].TargetId);

            var cancel = duel.CancelPlayerLinkFromSource(2);

            Assert.IsTrue(cancel.Success);
            Assert.IsFalse(duel.HasPlayerLinkFromSource(2));
        }

        [Test]
        public void ManualSwipeMode_PlayerCannotAttackMoreThanTwoDifferentTargets()
        {
            var duel = new HexHackDuelService(pvpConfig: CreateManualSwipeConfig(), seed: 7);
            duel.StartMatch();

            Assert.IsTrue(duel.TryAssignPlayerLinkFromSource(2, 0).Success);
            duel.Tick(4.1f);
            Assert.AreEqual(1, duel.Nodes[0].OwnerPlayerId);

            Assert.IsTrue(duel.TryAssignPlayerLinkFromSource(0, 1).Success);
            Assert.IsTrue(duel.TryAssignPlayerLinkFromSource(2, 3).Success);

            duel.Tick(4.1f);
            Assert.AreEqual(1, duel.Nodes[3].OwnerPlayerId);

            var thirdTarget = duel.TryAssignPlayerLinkFromSource(3, 4);

            Assert.IsFalse(thirdTarget.Success);
        }

        [Test]
        public void ManualSwipeMode_EnemyAiUsesAtMostTwoTargets()
        {
            var duel = new HexHackDuelService(pvpConfig: CreateManualSwipeConfig(), seed: 7);
            duel.StartMatch();

            duel.Tick(2.6f);

            var distinctTargets = new System.Collections.Generic.HashSet<int>();
            for (var i = 0; i < duel.EnemyLinks.Count; i++)
            {
                if (!duel.EnemyLinks[i].IsActive) continue;
                distinctTargets.Add(duel.EnemyLinks[i].TargetId);
            }

            Assert.LessOrEqual(distinctTargets.Count, 2);
        }

        private static PvpConfig CreateManualSwipeConfig()
        {
            var config = ScriptableObject.CreateInstance<PvpConfig>();
            config.HexHackDuelCombat = new PvpConfig.HexHackDuelCombatSection
            {
                AttackMode = HexHackDuelAttackMode.ManualSwipeSources,
                MaxConcurrentTargets = 2,
                MatchDurationSeconds = 30f,
                TickIntervalSeconds = 0.1f,
                BasePushPerSecond = 25f,
                AttackControlThreshold = 10f,
                OperationalMinPushScale = 0.35f,
                FlankBonusPerExtraAttacker = 0.25f,
                DefenseBonusPerFriendlyNeighbor = 0.20f,
                CorePushBonus = 0.30f,
                LowControlVulnerabilityThreshold = 35f,
                LowControlVulnerabilityMultiplier = 1.10f,
                OverdriveStartsAtSeconds = 25f,
                OverdriveMultiplier = 2f,
                EasyEnemyPowerRatio = 0.7f,
                HardEnemyPowerRatio = 1.3f,
                InitialAiDecisionDelaySeconds = 0f,
                AiDecisionDelayMinSeconds = 1.5f,
                AiDecisionDelayMaxSeconds = 2.5f
            };
            return config;
        }

        private static PvpConfig CreateSlowAiConfig()
        {
            var config = ScriptableObject.CreateInstance<PvpConfig>();
            config.HexHackDuelCombat = new PvpConfig.HexHackDuelCombatSection
            {
                AttackMode = HexHackDuelAttackMode.AutoAdjacentPressure,
                MaxConcurrentTargets = 2,
                MatchDurationSeconds = 30f,
                TickIntervalSeconds = 0.1f,
                BasePushPerSecond = 25f,
                AttackControlThreshold = 10f,
                OperationalMinPushScale = 0.35f,
                FlankBonusPerExtraAttacker = 0.25f,
                DefenseBonusPerFriendlyNeighbor = 0.20f,
                CorePushBonus = 0.30f,
                LowControlVulnerabilityThreshold = 35f,
                LowControlVulnerabilityMultiplier = 1.10f,
                OverdriveStartsAtSeconds = 25f,
                OverdriveMultiplier = 2f,
                EasyEnemyPowerRatio = 1f,
                HardEnemyPowerRatio = 1f,
                InitialAiDecisionDelaySeconds = 0f,
                AiDecisionDelayMinSeconds = 99f,
                AiDecisionDelayMaxSeconds = 99f
            };
            return config;
        }

        private static bool HasAnyActiveLink(System.Collections.Generic.IReadOnlyList<DuelLinkState> links)
        {
            for (var i = 0; i < links.Count; i++)
            {
                if (links[i].IsActive)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
