using AIWarsIdle.GameCore.Domain;
using AIWarsIdle.PvP.Config;
using AIWarsIdle.PvP.Services;
using NUnit.Framework;
using UnityEngine;

namespace AIWarsIdle.Tests.EditMode
{
    public sealed class Epic7PvpAttackChargesTests
    {
        private static PvpAttacksConfig CreateConfig(int maxAttacks = 5, int regenSeconds = 2 * 60 * 60)
        {
            var cfg = ScriptableObject.CreateInstance<PvpAttacksConfig>();
            cfg.MaxAttacks = maxAttacks;
            cfg.RegenSeconds = regenSeconds;
            cfg.AdExtraAttacksPerDay = 1;
            cfg.PremiumExtraAttacksPerPurchase = 0;
            cfg.PremiumCurrencyCostPerPurchase = 0;
            cfg.ValidateOrThrow();
            return cfg;
        }

        [Test]
        public void Regen_BeforeInterval_DoesNotAddAttack()
        {
            var state = new GameState { PvpAttacksRemaining = 0 };
            var svc = new PvpAttackChargesService(state, CreateConfig(regenSeconds: 7200));

            svc.Tick(nowUnixSeconds: 1000);
            Assert.AreEqual(0, state.PvpAttacksRemaining);
            Assert.AreEqual(1000 + 7200, state.NextPvpAttackRegenAtUnixSeconds);

            svc.Tick(nowUnixSeconds: 1000 + 7199);
            Assert.AreEqual(0, state.PvpAttacksRemaining);
        }

        [Test]
        public void Regen_UsesLastRegenTimestamp_WhenNextIsMissing()
        {
            var state = new GameState
            {
                PvpAttacksRemaining = 0,
                LastPvpAttackRegenUnixSeconds = 1000,
                NextPvpAttackRegenAtUnixSeconds = 0
            };
            var svc = new PvpAttackChargesService(state, CreateConfig(regenSeconds: 7200));

            svc.Tick(nowUnixSeconds: 1000 + 7199);
            Assert.AreEqual(0, state.PvpAttacksRemaining);
            Assert.AreEqual(1000 + 7200, state.NextPvpAttackRegenAtUnixSeconds);

            svc.Tick(nowUnixSeconds: 1000 + 7200);
            Assert.AreEqual(1, state.PvpAttacksRemaining);
        }

        [Test]
        public void Regen_AfterInterval_AddsOneAndClamps()
        {
            var state = new GameState { PvpAttacksRemaining = 4 };
            var svc = new PvpAttackChargesService(state, CreateConfig(regenSeconds: 7200));

            svc.Tick(nowUnixSeconds: 1000);
            Assert.AreEqual(4, state.PvpAttacksRemaining);
            Assert.AreEqual(1000 + 7200, state.NextPvpAttackRegenAtUnixSeconds);

            svc.Tick(nowUnixSeconds: 1000 + 7200);
            Assert.AreEqual(5, state.PvpAttacksRemaining);
            Assert.AreEqual(0, state.NextPvpAttackRegenAtUnixSeconds);
        }

        [Test]
        public void Regen_LongOffline_AddsMultipleButDoesNotExceedMax()
        {
            var state = new GameState { PvpAttacksRemaining = 0 };
            var svc = new PvpAttackChargesService(state, CreateConfig(maxAttacks: 5, regenSeconds: 100));

            svc.Tick(nowUnixSeconds: 1_000);
            svc.Tick(nowUnixSeconds: 1_000 + (10 * 100));

            Assert.AreEqual(5, state.PvpAttacksRemaining);
            Assert.AreEqual(0, state.NextPvpAttackRegenAtUnixSeconds);
        }

        [Test]
        public void NoBanking_WhenCapped_AndNextRegenIsInPast_DiscardsSchedule()
        {
            var state = new GameState
            {
                PvpAttacksRemaining = 5,
                NextPvpAttackRegenAtUnixSeconds = 1_000
            };
            var svc = new PvpAttackChargesService(state, CreateConfig(maxAttacks: 5, regenSeconds: 100));

            svc.Tick(nowUnixSeconds: 5_000);
            Assert.AreEqual(5, state.PvpAttacksRemaining);
            Assert.AreEqual(0, state.NextPvpAttackRegenAtUnixSeconds);

            Assert.IsTrue(svc.TrySpendOne(nowUnixSeconds: 5_000));
            Assert.AreEqual(4, state.PvpAttacksRemaining);
            Assert.AreEqual(5_000 + 100, state.NextPvpAttackRegenAtUnixSeconds);
        }

        [Test]
        public void BetterUx_WhenCappedByAdd_KeepsFutureNextRegenSoProgressResumesAfterSpend()
        {
            var state = new GameState
            {
                PvpAttacksRemaining = 4,
                NextPvpAttackRegenAtUnixSeconds = 2_000
            };
            var svc = new PvpAttackChargesService(state, CreateConfig(maxAttacks: 5, regenSeconds: 100));

            var added = svc.AddAttacks(nowUnixSeconds: 1_500, amount: 1);
            Assert.AreEqual(1, added);
            Assert.AreEqual(5, state.PvpAttacksRemaining);
            Assert.AreEqual(2_000, state.NextPvpAttackRegenAtUnixSeconds);

            Assert.IsTrue(svc.TrySpendOne(nowUnixSeconds: 1_600));
            Assert.AreEqual(4, state.PvpAttacksRemaining);
            Assert.AreEqual(2_000, state.NextPvpAttackRegenAtUnixSeconds);

            svc.Tick(nowUnixSeconds: 1_999);
            Assert.AreEqual(4, state.PvpAttacksRemaining);

            svc.Tick(nowUnixSeconds: 2_000);
            Assert.AreEqual(5, state.PvpAttacksRemaining);
        }

        [Test]
        public void Regen_Schedule_Recomputes_When_Config_RegenSeconds_Changes()
        {
            var state = new GameState
            {
                PvpAttacksRemaining = 4,
                NextPvpAttackRegenAtUnixSeconds = 1_000 + 7_200
            };
            var cfg = CreateConfig(maxAttacks: 5, regenSeconds: 10);
            var svc = new PvpAttackChargesService(state, cfg);

            svc.Tick(nowUnixSeconds: 1_000);
            Assert.AreEqual(1_010, state.NextPvpAttackRegenAtUnixSeconds);

            svc.Tick(nowUnixSeconds: 1_009);
            Assert.AreEqual(4, state.PvpAttacksRemaining);

            svc.Tick(nowUnixSeconds: 1_010);
            Assert.AreEqual(5, state.PvpAttacksRemaining);
        }

        [Test]
        public void SpendOne_StartsCountdown_And_DoesNotResetIt_OnEveryTick()
        {
            var state = new GameState { PvpAttacksRemaining = 5 };
            var svc = new PvpAttackChargesService(state, CreateConfig(maxAttacks: 5, regenSeconds: 10));

            Assert.IsTrue(svc.TrySpendOne(nowUnixSeconds: 1_000));
            Assert.AreEqual(4, state.PvpAttacksRemaining);
            Assert.AreEqual(1_010, state.NextPvpAttackRegenAtUnixSeconds);

            svc.Tick(nowUnixSeconds: 1_005);
            Assert.AreEqual(4, state.PvpAttacksRemaining);
            Assert.AreEqual(1_010, state.NextPvpAttackRegenAtUnixSeconds);

            svc.Tick(nowUnixSeconds: 1_010);
            Assert.AreEqual(5, state.PvpAttacksRemaining);
        }

        [Test]
        public void SpendOne_Replaces_StalePastSchedule_Instead_Of_InstantlyRegenerating()
        {
            var state = new GameState
            {
                PvpAttacksRemaining = 5,
                NextPvpAttackRegenAtUnixSeconds = 900
            };
            var svc = new PvpAttackChargesService(state, CreateConfig(maxAttacks: 5, regenSeconds: 10));

            Assert.IsTrue(svc.TrySpendOne(nowUnixSeconds: 1_000));
            Assert.AreEqual(4, state.PvpAttacksRemaining);
            Assert.AreEqual(1_010, state.NextPvpAttackRegenAtUnixSeconds);

            Assert.AreEqual(4, svc.GetRemaining(nowUnixSeconds: 1_000));
            Assert.AreEqual(1_010, state.NextPvpAttackRegenAtUnixSeconds);
        }

        [Test]
        public void SpendOne_With_StaleLastRegen_DoesNotRefillBackToFourOnRepeatedSameTimestamp()
        {
            var state = new GameState
            {
                PvpAttacksRemaining = 5,
                LastPvpAttackRegenUnixSeconds = 120,
                NextPvpAttackRegenAtUnixSeconds = 180
            };
            var svc = new PvpAttackChargesService(state, CreateConfig(maxAttacks: 5, regenSeconds: 60));

            Assert.IsTrue(svc.TrySpendOne(nowUnixSeconds: 1_000));
            Assert.AreEqual(4, state.PvpAttacksRemaining);
            Assert.AreEqual(1_000, state.LastPvpAttackRegenUnixSeconds);
            Assert.AreEqual(1_060, state.NextPvpAttackRegenAtUnixSeconds);

            Assert.IsTrue(svc.TrySpendOne(nowUnixSeconds: 1_000));
            Assert.AreEqual(3, state.PvpAttacksRemaining);
            Assert.AreEqual(1_000, state.LastPvpAttackRegenUnixSeconds);
            Assert.AreEqual(1_060, state.NextPvpAttackRegenAtUnixSeconds);
        }

        [Test]
        public void DailyAdClaim_UsesUtcDayBoundary()
        {
            var state = new GameState { PvpAttacksRemaining = 0, LastPvpAdAttackClaimUnixSeconds = 0 };
            var svc = new PvpAttackChargesService(state, CreateConfig(maxAttacks: 5, regenSeconds: 100));

            Assert.IsTrue(svc.TryClaimDailyAdAttack(nowUnixSeconds: 1));
            Assert.AreEqual(1, state.PvpAttacksRemaining);
            Assert.AreEqual(1, state.LastPvpAdAttackClaimUnixSeconds);

            Assert.IsFalse(svc.TryClaimDailyAdAttack(nowUnixSeconds: 10));
            Assert.AreEqual(1, state.PvpAttacksRemaining);

            // cross UTC day boundary: 86399 -> day 0, 86400 -> day 1
            // Use a fresh state with regen scheduled far in the future so Tick() won't cap attacks and block the claim.
            var state2 = new GameState
            {
                PvpAttacksRemaining = 0,
                LastPvpAdAttackClaimUnixSeconds = 86_399,
                NextPvpAttackRegenAtUnixSeconds = 86_400 + 1_000_000_000L
            };
            var svc2 = new PvpAttackChargesService(state2, CreateConfig(maxAttacks: 5, regenSeconds: 100));
            Assert.IsTrue(svc2.TryClaimDailyAdAttack(nowUnixSeconds: 86_400));
        }
    }
}
