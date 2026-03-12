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
    }
}
