using System;
using System.IO;
using AIWarsIdle.Persistence.Domain;
using AIWarsIdle.Persistence.IO;
using AIWarsIdle.Persistence.Services;
using NUnit.Framework;
using Newtonsoft.Json;

namespace AIWarsIdle.Tests
{
    public sealed class SaveServiceTests
    {
        private static SaveDataV1 CreateSampleSave()
        {
            return new SaveDataV1
            {
                SoftCurrency = 123.45,
                LifetimeEarnedSoftCurrency = 1000.0,
                PremiumCurrency = 7,
                PrestigeCount = 2,
                PermanentUpgradeLevel = 1,
                LeagueSeasonId = 10,
                League = 3,
                SeasonPoints = 42,
                PvpAttacksRemaining = 4,
                LastPvpAttackRegenUnixSeconds = 100,
                LastPvpAdAttackClaimUnixSeconds = 200,
                MapSeasonId = 5,
                Sectors = new[]
                {
                    new SectorSaveData
                    {
                        SectorId = 1,
                        OwnerPlayerId = 0,
                        OwnerPvpPower = 12.3,
                        OwnerLeague = 2,
                        OwnerSeasonPoints = 11,
                        Stability = 55f,
                        LastCombatUnixSeconds = 999,
                        CapturedUnixSeconds = 500
                    }
                },
                Overclock = new OverclockSaveData
                {
                    Charges = 1,
                    ActiveUntilUnixSeconds = 1234,
                    NextChargeAtUnixSeconds = 1300
                },
                LastLoginUnixSeconds = 300
            };
        }

        [Test]
        public void SaveThenLoad_Returns_Identical_State()
        {
            var dir = Path.Combine(Path.GetTempPath(), "aiwarsidle-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);

            try
            {
                var service = new SaveService(
                    new SaveFilePaths(dir, "save"),
                    new SaveDataMigrator());

                var input = CreateSampleSave();
                service.Save(input);

                var loaded = service.LoadOrCreate();

                var inputJson = JsonConvert.SerializeObject(input);
                var loadedJson = JsonConvert.SerializeObject(loaded);
                Assert.AreEqual(inputJson, loadedJson);
            }
            finally
            {
                try { Directory.Delete(dir, recursive: true); } catch { }
            }
        }

        [Test]
        public void Corrupt_Primary_Save_Falls_Back_To_Backup_Without_Crash()
        {
            var dir = Path.Combine(Path.GetTempPath(), "aiwarsidle-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);

            try
            {
                var paths = new SaveFilePaths(dir, "save");
                var service = new SaveService(paths, new SaveDataMigrator());

                var input = CreateSampleSave();
                service.Save(input);

                // second save creates backup via File.Replace/copy-fallback
                input.SoftCurrency = 999;
                service.Save(input);

                // corrupt primary
                File.WriteAllText(paths.SavePath, "{not valid json");

                var loaded = service.LoadOrCreate();

                Assert.AreEqual(999, loaded.SoftCurrency);
            }
            finally
            {
                try { Directory.Delete(dir, recursive: true); } catch { }
            }
        }
    }
}
