using AIWarsIdle.GameCore.Domain;
using AIWarsIdle.Persistence.Domain;
using NUnit.Framework;

namespace AIWarsIdle.Tests
{
    public sealed class SaveDataV1SchemaTests
    {
        [Test]
        public void SaveDataV1_Defaults_Are_NonNull_And_Have_Correct_Lengths()
        {
            var save = new SaveDataV1();

            Assert.AreEqual(1, save.Version);
            Assert.AreEqual(0, save.LifetimeEarnedSoftCurrency);
            Assert.NotNull(save.GeneratorLevels);
            Assert.AreEqual(GameState.GeneratorCount, save.GeneratorLevels.Length);

            Assert.NotNull(save.Sectors);
            Assert.NotNull(save.Overclock);
        }

        [Test]
        public void SectorSaveData_Normalize_Clamps_Stability_And_Timestamps()
        {
            var sector = new SectorSaveData
            {
                SectorId = 1,
                Stability = 123f,
                LastCombatUnixSeconds = -5,
                CapturedUnixSeconds = -10
            };

            sector.Normalize();

            Assert.AreEqual(100f, sector.Stability);
            Assert.AreEqual(0, sector.LastCombatUnixSeconds);
            Assert.AreEqual(0, sector.CapturedUnixSeconds);

            sector.Stability = -1f;
            sector.Normalize();
            Assert.AreEqual(0f, sector.Stability);
        }

        [Test]
        public void OverclockSaveData_Defaults_To_Two_Charges_And_No_Activity()
        {
            var overclock = new OverclockSaveData();

            Assert.AreEqual(2, overclock.Charges);
            Assert.AreEqual(0, overclock.ActiveUntilUnixSeconds);
            Assert.AreEqual(0, overclock.NextChargeAtUnixSeconds);
        }

        [Test]
        public void OverclockSaveData_Normalize_DoesNotClamp_Charges_To_Two()
        {
            var overclock = new OverclockSaveData
            {
                Charges = 999,
                ActiveUntilUnixSeconds = 0,
                NextChargeAtUnixSeconds = 0
            };

            overclock.Normalize();

            Assert.AreEqual(999, overclock.Charges);
        }

        [Test]
        public void SaveDataV1_Normalize_Fixes_Nulls_And_Negative_Timestamps()
        {
            var save = new SaveDataV1
            {
                GeneratorLevels = null!,
                Sectors = null!,
                Overclock = null!,
                LifetimeEarnedSoftCurrency = -1,
                LastLoginUnixSeconds = -1,
                LastPvpAdAttackClaimUnixSeconds = -2,
                LastPvpAttackRegenUnixSeconds = -3,
                NextPvpAttackRegenAtUnixSeconds = -4
            };

            save.Normalize();

            Assert.NotNull(save.GeneratorLevels);
            Assert.AreEqual(GameState.GeneratorCount, save.GeneratorLevels.Length);
            Assert.NotNull(save.Sectors);
            Assert.NotNull(save.Overclock);

            Assert.AreEqual(0, save.LifetimeEarnedSoftCurrency);
            Assert.AreEqual(0, save.LastLoginUnixSeconds);
            Assert.AreEqual(0, save.LastPvpAdAttackClaimUnixSeconds);
            Assert.AreEqual(0, save.LastPvpAttackRegenUnixSeconds);
            Assert.AreEqual(0, save.NextPvpAttackRegenAtUnixSeconds);
        }
    }
}
