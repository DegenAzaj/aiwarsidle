using AIWarsIdle.PvP.Services;
using NUnit.Framework;

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
        public void StartMatch_UsesMediumDifficultyAsEqualPower()
        {
            var duel = new HexHackDuelService();

            duel.StartMatch();

            Assert.AreEqual(DuelDifficulty.Medium, duel.Difficulty);
            Assert.AreEqual(duel.PlayerPower, duel.EnemyPower, 0.001d);
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
    }
}
