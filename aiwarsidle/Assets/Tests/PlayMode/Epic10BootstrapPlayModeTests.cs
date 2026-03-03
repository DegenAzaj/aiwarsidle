using System;
using System.IO;
using System.Collections;
using AIWarsIdle.Bootstrap;
using AIWarsIdle.GameCore.Config;
using AIWarsIdle.PvP.Config;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AIWarsIdle.Tests
{
    public sealed class Epic10BootstrapPlayModeTests
    {
        [UnityTest]
        public IEnumerator Bootstrapper_Starts_And_Production_Ticks()
        {
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            var balance = ScriptableObject.CreateInstance<BalanceConfig>();
            balance.GeneratorBaseCosts = new[] { 10.0, 10.0, 10.0, 10.0, 10.0 };
            balance.GeneratorCostGrowthFactors = new[] { 1.15, 1.15, 1.15, 1.15, 1.15 };
            balance.GeneratorBaseOutputs = new[] { 1.0, 0.5, 0.25, 0.1, 0.05 };
            balance.OfflineCapSeconds = 12 * 60 * 60;

            var overclock = ScriptableObject.CreateInstance<OverclockConfig>();
            overclock.DurationSeconds = 10;
            overclock.RegenSeconds = 90;
            overclock.MaxCharges = 2;

            var map = ScriptableObject.CreateInstance<MapConfig>();
            map.LocalPlayerId = 1;
            map.MapSeasonLengthDays = 7;
            map.MapSeasonAnchorUnixSecondsUtc = 0;
            map.HomeSectorId = 0;
            map.SectorDefinitions = Array.Empty<MapConfig.SectorDefinition>();
            map.Adjacency = Array.Empty<MapConfig.SectorEdge>();

            var saveSubdir = $"epic10_test_{Guid.NewGuid():N}";
            var saveDir = Path.Combine(Application.persistentDataPath, saveSubdir);
            Directory.CreateDirectory(saveDir);

            // Seed the save file without referencing Newtonsoft in this test assembly.
            // SaveService uses Newtonsoft internally to load this JSON.
            var json = "{"
                       + "\"Version\":1,"
                       + "\"SoftCurrency\":0,"
                       + "\"LifetimeEarnedSoftCurrency\":0,"
                       + "\"PremiumCurrency\":0,"
                       + "\"GeneratorLevels\":[1,0,0,0,0],"
                       + "\"LastLoginUnixSeconds\":" + now + ","
                       + "\"PendingOfflineGain\":0,"
                       + "\"MapSeasonId\":0,"
                       + "\"Sectors\":[],"
                       + "\"Overclock\":{\"Charges\":2,\"ActiveUntilUnixSeconds\":0,\"NextChargeAtUnixSeconds\":0}"
                       + "}";
            File.WriteAllText(Path.Combine(saveDir, "save.json"), json);

            var go = new GameObject("Epic10Bootstrapper");
            var bootstrap = go.AddComponent<GameBootstrapper>();
            bootstrap.BalanceConfig = balance;
            bootstrap.OverclockConfig = overclock;
            bootstrap.MapConfig = map;
            bootstrap.SetSaveLocationForTests(saveSubdir, "save");

            yield return null; // allow Start() to run

            Assert.NotNull(bootstrap.Loop);
            Assert.NotNull(bootstrap.Loop.State);

            var startCurrency = bootstrap.Loop.State.SoftCurrency;
            yield return new WaitForSeconds(1.2f);
            var afterCurrency = bootstrap.Loop.State.SoftCurrency;

            Assert.Greater(afterCurrency, startCurrency);
        }
    }
}
