using System;
using System.Collections;
using System.IO;
using AIWarsIdle.Bootstrap;
using AIWarsIdle.GameCore.Config;
using AIWarsIdle.PvP.Config;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace AIWarsIdle.Tests
{
    public sealed class Epic11OfflineResumePlayModeTests
    {
        [UnityTest]
        public IEnumerator Resume_FromPause_Banks_OfflineGain_And_ActivatesClaimButton()
        {
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var saveSubdir = $"epic11_resume_{Guid.NewGuid():N}";
            var saveDir = Path.Combine(Application.persistentDataPath, saveSubdir);
            Directory.CreateDirectory(saveDir);
            GameObject hudClaimButtonGo = null;
            GameObject modalRoot = null;
            GameObject bootstrapGo = null;
            BalanceConfig balance = null;
            OverclockConfig overclock = null;
            MapConfig map = null;
            PvpAttacksConfig pvp = null;

            try
            {
                balance = ScriptableObject.CreateInstance<BalanceConfig>();
                balance.GeneratorBaseCosts = new[] { 10.0, 10.0, 10.0, 10.0, 10.0 };
                balance.GeneratorCostGrowthFactors = new[] { 1.15, 1.15, 1.15, 1.15, 1.15 };
                balance.GeneratorBaseOutputs = new[] { 1.0, 0.5, 0.25, 0.1, 0.05 };
                balance.OfflineCapSeconds = 12 * 60 * 60;
                balance.OfflineEfficiency = 0.6;

                overclock = ScriptableObject.CreateInstance<OverclockConfig>();
                overclock.DurationSeconds = 10;
                overclock.RegenSeconds = 90;
                overclock.MaxCharges = 2;

                map = ScriptableObject.CreateInstance<MapConfig>();
                map.LocalPlayerId = 1;
                map.MapSeasonLengthDays = 7;
                map.MapSeasonAnchorUnixSecondsUtc = 0;
                map.HomeSectorId = 0;
                map.SectorDefinitions = Array.Empty<MapConfig.SectorDefinition>();
                map.Adjacency = Array.Empty<MapConfig.SectorEdge>();

                pvp = ScriptableObject.CreateInstance<PvpAttacksConfig>();
                pvp.MaxAttacks = 5;
                pvp.RegenSeconds = 60;

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

                hudClaimButtonGo = new GameObject("button_claim_offline", typeof(RectTransform), typeof(Image), typeof(Button));
                modalRoot = new GameObject("claim_offline_root");
                modalRoot.SetActive(false);
                new GameObject("offline_time_description", typeof(RectTransform), typeof(TMPro.TextMeshProUGUI)).transform.SetParent(modalRoot.transform, false);
                new GameObject("offline_gain_number", typeof(RectTransform), typeof(TMPro.TextMeshProUGUI)).transform.SetParent(modalRoot.transform, false);
                new GameObject("button_claim", typeof(RectTransform), typeof(Image), typeof(Button)).transform.SetParent(modalRoot.transform, false);
                new GameObject("button_claim_x2", typeof(RectTransform), typeof(Image), typeof(Button)).transform.SetParent(modalRoot.transform, false);
                new GameObject("button_no_thanks", typeof(RectTransform), typeof(Image), typeof(Button)).transform.SetParent(modalRoot.transform, false);

                bootstrapGo = new GameObject("Epic11ResumeBootstrapper");
                var bootstrap = bootstrapGo.AddComponent<GameBootstrapper>();
                bootstrap.BalanceConfig = balance;
                bootstrap.OverclockConfig = overclock;
                bootstrap.MapConfig = map;
                bootstrap.PvpAttacksConfig = pvp;
                bootstrap.SetSaveLocationForTests(saveSubdir, "save");
                _ = bootstrapGo.AddComponent<UI.Generators.OfflineClaimModalController>();

                yield return null;
                bootstrap.Initialize();
                yield return null;

                Assert.AreEqual(0d, bootstrap.Loop.OfflineClaim.PendingOfflineGain);
                Assert.IsFalse(hudClaimButtonGo.GetComponent<UnityEngine.UI.Button>().interactable);

                bootstrap.Loop.OnApplicationPause(true);
                var pausedAt = bootstrap.Loop.NowUnixSeconds;
                yield return WaitForUnixSecondAdvance(bootstrap, pausedAt);
                bootstrap.Loop.OnApplicationPause(false);
                yield return null;
                yield return null;

                Assert.Greater(bootstrap.Loop.OfflineClaim.PendingOfflineGain, 0d);
                Assert.IsTrue(hudClaimButtonGo.GetComponent<UnityEngine.UI.Button>().interactable);
            }
            finally
            {
                TryDeleteDirectory(saveDir);
                DestroyIfExists(bootstrapGo);
                DestroyIfExists(modalRoot);
                DestroyIfExists(hudClaimButtonGo);
                DestroyIfExists(balance);
                DestroyIfExists(overclock);
                DestroyIfExists(map);
                DestroyIfExists(pvp);
            }
        }

        private static void TryDeleteDirectory(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return;
            if (!Directory.Exists(path)) return;

            try
            {
                Directory.Delete(path, recursive: true);
            }
            catch
            {
            }
        }

        private static void DestroyIfExists(UnityEngine.Object obj)
        {
            if (obj == null) return;
            UnityEngine.Object.Destroy(obj);
        }

        private static IEnumerator WaitForUnixSecondAdvance(GameBootstrapper bootstrap, long pausedAtUnixSeconds)
        {
            const float timeoutSeconds = 2.5f;
            var startedAt = Time.realtimeSinceStartup;

            while (bootstrap != null && bootstrap.Loop != null && bootstrap.Loop.NowUnixSeconds <= pausedAtUnixSeconds)
            {
                if (Time.realtimeSinceStartup - startedAt > timeoutSeconds)
                {
                    Assert.Fail($"Timed out waiting for Unix second to advance past {pausedAtUnixSeconds}.");
                }

                yield return null;
            }
        }
    }
}
