using System;
using System.IO;
using System.Collections;
using AIWarsIdle.Bootstrap;
using AIWarsIdle.GameCore.Config;
using AIWarsIdle.PvP.Config;
using AIWarsIdle.UI.Generators;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace AIWarsIdle.Tests
{
    public sealed class Epic11OfflineClaimModalPlayModeTests
    {
        [UnityTest]
        public IEnumerator PendingOfflineGain_ShowsModal_AfterSplashCloses()
        {
            var saveSubdir = $"epic11_offline_modal_show_{Guid.NewGuid():N}";
            var saveDir = Path.Combine(Application.persistentDataPath, saveSubdir);
            Directory.CreateDirectory(saveDir);

            try
            {
                var bootstrap = CreateBootstrapper(saveSubdir, SeedSaveJson(lastLoginOffsetSeconds: 3 * 60 * 60 + 25 * 60 + 43));
                var splash = new GameObject("SplashScreen");
                splash.SetActive(true);
                var modal = CreateOfflineModal();
                _ = bootstrap.gameObject.AddComponent<OfflineClaimModalController>();

                yield return null;
                bootstrap.Initialize();
                yield return null;

                Assert.IsFalse(modal.Root.activeSelf, "Modal should stay hidden while Splash is visible.");

                splash.SetActive(false);
                yield return null;
                yield return null;

                Assert.IsTrue(modal.Root.activeSelf, "Modal should appear after entering Hub.");
                Assert.IsTrue(modal.HudClaimButton.interactable, "HUD claim button should be enabled when offline reward is pending.");
                Assert.AreEqual(UiFormat.Compact(bootstrap.Loop.OfflineClaim.PendingOfflineGain), modal.GainText.text);
                StringAssert.StartsWith("Offline Time: ", modal.TimeText.text);
                StringAssert.Contains("(CAP: 12h)", modal.TimeText.text);
            }
            finally
            {
                TryDeleteDirectory(saveDir);
                DestroyAllTestObjects();
            }
        }

        [UnityTest]
        public IEnumerator ClaimX1_ClearsPending_AddsCurrency_AndHidesModal()
        {
            var saveSubdir = $"epic11_offline_modal_claim1_{Guid.NewGuid():N}";
            var saveDir = Path.Combine(Application.persistentDataPath, saveSubdir);
            Directory.CreateDirectory(saveDir);

            try
            {
                var bootstrap = CreateBootstrapper(saveSubdir, SeedSaveJson(lastLoginOffsetSeconds: 3600));
                var modal = CreateOfflineModal();
                _ = bootstrap.gameObject.AddComponent<OfflineClaimModalController>();

                yield return null;
                bootstrap.Initialize();
                yield return null;
                yield return null;

                var pending = bootstrap.Loop.OfflineClaim.PendingOfflineGain;
                Assert.Greater(pending, 0);
                Assert.IsTrue(modal.Root.activeSelf);
                Assert.IsTrue(modal.HudClaimButton.interactable);

                modal.ClaimButton.onClick.Invoke();
                yield return null;

                Assert.AreEqual(0d, bootstrap.Loop.OfflineClaim.PendingOfflineGain);
                Assert.That(bootstrap.Loop.State.SoftCurrency, Is.EqualTo(pending).Within(0.001d));
                Assert.IsFalse(modal.Root.activeSelf);
                Assert.IsFalse(modal.HudClaimButton.interactable);
            }
            finally
            {
                TryDeleteDirectory(saveDir);
                DestroyAllTestObjects();
            }
        }

        [UnityTest]
        public IEnumerator ClaimX2_UsesRewardedStub_AddsDouble_AndHidesModal()
        {
            var saveSubdir = $"epic11_offline_modal_claim2_{Guid.NewGuid():N}";
            var saveDir = Path.Combine(Application.persistentDataPath, saveSubdir);
            Directory.CreateDirectory(saveDir);

            try
            {
                var bootstrap = CreateBootstrapper(saveSubdir, SeedSaveJson(lastLoginOffsetSeconds: 3600));
                var modal = CreateOfflineModal();
                _ = bootstrap.gameObject.AddComponent<OfflineClaimModalController>();

                yield return null;
                bootstrap.Initialize();
                yield return null;
                yield return null;

                var pending = bootstrap.Loop.OfflineClaim.PendingOfflineGain;
                Assert.Greater(pending, 0);

                modal.ClaimX2Button.onClick.Invoke();
                yield return null;

                Assert.AreEqual(0d, bootstrap.Loop.OfflineClaim.PendingOfflineGain);
                Assert.That(bootstrap.Loop.State.SoftCurrency, Is.EqualTo(pending * 2d).Within(0.001d));
                Assert.IsFalse(modal.Root.activeSelf);
            }
            finally
            {
                TryDeleteDirectory(saveDir);
                DestroyAllTestObjects();
            }
        }

        [UnityTest]
        public IEnumerator NotNow_KeepsPending_AndHidesModal()
        {
            var saveSubdir = $"epic11_offline_modal_notnow_{Guid.NewGuid():N}";
            var saveDir = Path.Combine(Application.persistentDataPath, saveSubdir);
            Directory.CreateDirectory(saveDir);

            try
            {
                var bootstrap = CreateBootstrapper(saveSubdir, SeedSaveJson(lastLoginOffsetSeconds: 3600));
                var modal = CreateOfflineModal();
                _ = bootstrap.gameObject.AddComponent<OfflineClaimModalController>();

                yield return null;
                bootstrap.Initialize();
                yield return null;
                yield return null;

                var pending = bootstrap.Loop.OfflineClaim.PendingOfflineGain;
                modal.NotNowButton.onClick.Invoke();
                yield return null;

                Assert.That(bootstrap.Loop.OfflineClaim.PendingOfflineGain, Is.EqualTo(pending).Within(0.001d));
                Assert.AreEqual(0d, bootstrap.Loop.State.SoftCurrency);
                Assert.IsFalse(modal.Root.activeSelf);
                Assert.IsTrue(modal.HudClaimButton.interactable);
            }
            finally
            {
                TryDeleteDirectory(saveDir);
                DestroyAllTestObjects();
            }
        }

        [UnityTest]
        public IEnumerator HudClaimButton_ReopensModal_AfterNotNow()
        {
            var saveSubdir = $"epic11_offline_modal_hud_reopen_{Guid.NewGuid():N}";
            var saveDir = Path.Combine(Application.persistentDataPath, saveSubdir);
            Directory.CreateDirectory(saveDir);

            try
            {
                var bootstrap = CreateBootstrapper(saveSubdir, SeedSaveJson(lastLoginOffsetSeconds: 3600));
                var modal = CreateOfflineModal();
                _ = bootstrap.gameObject.AddComponent<OfflineClaimModalController>();

                yield return null;
                bootstrap.Initialize();
                yield return null;
                yield return null;

                Assert.IsTrue(modal.Root.activeSelf);
                Assert.IsTrue(modal.HudClaimButton.interactable);

                modal.NotNowButton.onClick.Invoke();
                yield return null;

                Assert.IsFalse(modal.Root.activeSelf);
                Assert.IsTrue(modal.HudClaimButton.interactable);

                modal.HudClaimButton.onClick.Invoke();
                yield return null;

                Assert.IsTrue(modal.Root.activeSelf, "HUD claim button should reopen the offline modal.");
            }
            finally
            {
                TryDeleteDirectory(saveDir);
                DestroyAllTestObjects();
            }
        }

        private static GameBootstrapper CreateBootstrapper(string saveSubdir, string seedJson)
        {
            var balance = ScriptableObject.CreateInstance<BalanceConfig>();
            balance.GeneratorBaseCosts = new[] { 10.0, 10.0, 10.0, 10.0, 10.0 };
            balance.GeneratorCostGrowthFactors = new[] { 1.15, 1.15, 1.15, 1.15, 1.15 };
            balance.GeneratorBaseOutputs = new[] { 1.0, 0.5, 0.25, 0.1, 0.05 };
            balance.OfflineCapSeconds = 12 * 60 * 60;
            balance.OfflineEfficiency = 0.6;

            var overclock = ScriptableObject.CreateInstance<OverclockConfig>();
            overclock.DurationSeconds = 10;
            overclock.RegenSeconds = 90;
            overclock.MaxCharges = 2;
            overclock.ProductionMultiplier = 3;

            var map = ScriptableObject.CreateInstance<MapConfig>();
            map.LocalPlayerId = 1;
            map.MapSeasonLengthDays = 7;
            map.MapSeasonAnchorUnixSecondsUtc = 0;
            map.HomeSectorId = 0;
            map.SectorDefinitions = Array.Empty<MapConfig.SectorDefinition>();
            map.Adjacency = Array.Empty<MapConfig.SectorEdge>();

            var pvp = ScriptableObject.CreateInstance<PvpAttacksConfig>();
            pvp.MaxAttacks = 5;
            pvp.RegenSeconds = 60;

            var saveDir = Path.Combine(Application.persistentDataPath, saveSubdir);
            Directory.CreateDirectory(saveDir);
            File.WriteAllText(Path.Combine(saveDir, "save.json"), seedJson);

            var go = new GameObject("Epic11OfflineBootstrapper");
            var bootstrap = go.AddComponent<GameBootstrapper>();
            bootstrap.BalanceConfig = balance;
            bootstrap.OverclockConfig = overclock;
            bootstrap.MapConfig = map;
            bootstrap.PvpAttacksConfig = pvp;
            bootstrap.SetSaveLocationForTests(saveSubdir, "save");
            return bootstrap;
        }

        private static OfflineModalRefs CreateOfflineModal()
        {
            var root = new GameObject("claim_offline_root");
            root.SetActive(false);

            var hudClaimButton = CreateButton("button_claim_offline", parent: null);

            var timeGo = new GameObject("offline_time_description", typeof(RectTransform), typeof(TextMeshProUGUI));
            timeGo.transform.SetParent(root.transform, false);
            var timeText = timeGo.GetComponent<TextMeshProUGUI>();

            var gainGo = new GameObject("offline_gain_number", typeof(RectTransform), typeof(TextMeshProUGUI));
            gainGo.transform.SetParent(root.transform, false);
            var gainText = gainGo.GetComponent<TextMeshProUGUI>();

            var claimX2 = CreateButton("button_claim_x2", root.transform);
            var claim = CreateButton("button_claim", root.transform);
            var notNow = CreateButton("button_no_thanks", root.transform);

            return new OfflineModalRefs(root, gainText, timeText, claim, claimX2, notNow, hudClaimButton);
        }

        private static Button CreateButton(string name, Transform parent)
        {
            var buttonGo = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            if (parent != null) buttonGo.transform.SetParent(parent, false);
            return buttonGo.GetComponent<Button>();
        }

        private static string SeedSaveJson(long lastLoginOffsetSeconds)
        {
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            return "{"
                   + "\"Version\":1,"
                   + "\"SoftCurrency\":0,"
                   + "\"LifetimeEarnedSoftCurrency\":0,"
                   + "\"PremiumCurrency\":0,"
                   + "\"GeneratorLevels\":[1,0,0,0,0],"
                   + "\"LastLoginUnixSeconds\":" + (now - lastLoginOffsetSeconds) + ","
                   + "\"PendingOfflineGain\":0,"
                   + "\"LastBankedOfflineRawSeconds\":0,"
                   + "\"LastBankedOfflineEffectiveSeconds\":0,"
                   + "\"MapSeasonId\":0,"
                   + "\"Sectors\":[],"
                   + "\"Overclock\":{\"Charges\":2,\"ActiveUntilUnixSeconds\":0,\"NextChargeAtUnixSeconds\":0}"
                   + "}";
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

        private static void DestroyAllTestObjects()
        {
            var roots = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
            for (var i = 0; i < roots.Length; i++)
            {
                var go = roots[i];
                if (go == null) continue;
                if (go.hideFlags != HideFlags.None) continue;
                UnityEngine.Object.Destroy(go);
            }
        }

        private readonly struct OfflineModalRefs
        {
            public OfflineModalRefs(GameObject root, TMP_Text gainText, TMP_Text timeText, Button claimButton, Button claimX2Button, Button notNowButton, Button hudClaimButton)
            {
                Root = root;
                GainText = gainText;
                TimeText = timeText;
                ClaimButton = claimButton;
                ClaimX2Button = claimX2Button;
                NotNowButton = notNowButton;
                HudClaimButton = hudClaimButton;
            }

            public GameObject Root { get; }
            public TMP_Text GainText { get; }
            public TMP_Text TimeText { get; }
            public Button ClaimButton { get; }
            public Button ClaimX2Button { get; }
            public Button NotNowButton { get; }
            public Button HudClaimButton { get; }
        }
    }
}
