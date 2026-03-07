using System;
using System.Collections;
using System.IO;
using System.Reflection;
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
    public sealed class Epic04OverclockAndUpgradePlayModeTests
    {
        [UnityTest]
        public IEnumerator ClickUpgrade_IncreasesLevel_AndPpsOnHud()
        {
            var saveSubdir = $"epic04_upgrade_{Guid.NewGuid():N}";
            var saveDir = Path.Combine(Application.persistentDataPath, saveSubdir);
            Directory.CreateDirectory(saveDir);

            try
            {
                var bootstrap = CreateBootstrapper(
                    saveSubdir,
                    SeedSaveJson(
                        softCurrency: 1_000,
                        generatorLevels: new[] { 1, 0, 0, 0, 0 },
                        charges: 2,
                        activeUntil: 0,
                        nextChargeAt: 0));

                var context = CreateContext(bootstrap);

                var hud = CreateHud(context, out var hudPpsText);
                var row = CreateGeneratorRow(context, generatorId: 0, out var upgradeButton);

                yield return null; // allow Start/Awake/OnEnable

                bootstrap.Initialize();

                yield return null;
                hud.Refresh();
                row.Refresh();

                var now = bootstrap.Loop.NowUnixSeconds;
                var ppsBefore = bootstrap.Loop.Production.CalculateProductionPerSecond(now);
                var levelBefore = bootstrap.Loop.State.GeneratorLevels[0];

                upgradeButton.onClick.Invoke();
                yield return null;

                hud.Refresh();
                row.Refresh();

                var ppsAfter = bootstrap.Loop.Production.CalculateProductionPerSecond(bootstrap.Loop.NowUnixSeconds);
                var levelAfter = bootstrap.Loop.State.GeneratorLevels[0];

                Assert.AreEqual(levelBefore + 1, levelAfter, "Generator level should increase by 1 after upgrade click.");
                Assert.Greater(ppsAfter, ppsBefore, "PPS should increase after upgrading a generator.");
                Assert.AreEqual(UiFormat.Compact(ppsAfter), hudPpsText.text, "HUD PPS text should reflect increased PPS.");
            }
            finally
            {
                TryDeleteDirectory(saveDir);
                DestroyAllTestObjects();
            }
        }

        [UnityTest]
        public IEnumerator ClickOverclock_MultipliesPpsX3_For10Seconds_ThenReturns()
        {
            var saveSubdir = $"epic04_overclock_{Guid.NewGuid():N}";
            var saveDir = Path.Combine(Application.persistentDataPath, saveSubdir);
            Directory.CreateDirectory(saveDir);

            try
            {
                var bootstrap = CreateBootstrapper(
                    saveSubdir,
                    SeedSaveJson(
                        softCurrency: 0,
                        generatorLevels: new[] { 1, 0, 0, 0, 0 },
                        charges: 2,
                        activeUntil: 0,
                        nextChargeAt: 0));

                var context = CreateContext(bootstrap);

                _ = CreateHud(context, out _);
                var overclock = CreateOverclockButton(context, out var overclockButton);

                yield return null;
                bootstrap.Initialize();
                yield return null;

                var now = bootstrap.Loop.NowUnixSeconds;
                var ppsBase = bootstrap.Loop.Production.CalculateProductionPerSecond(now);

                overclockButton.onClick.Invoke();
                yield return null;

                var nowAfterClick = bootstrap.Loop.NowUnixSeconds;
                Assert.IsTrue(bootstrap.Loop.Overclock.IsActive(nowAfterClick), "Overclock should be active right after clicking the button.");
                Assert.AreEqual(10, bootstrap.Loop.State.Overclock.ActiveUntilUnixSeconds - nowAfterClick, "Overclock duration should be 10 seconds.");

                var ppsActive = bootstrap.Loop.Production.CalculateProductionPerSecond(nowAfterClick);
                Assert.That(ppsActive, Is.EqualTo(ppsBase * 3.0).Within(1e-4), "Overclock should multiply PPS by ×3 when active.");

                yield return new WaitForSeconds(11.0f);

                var nowAfter = bootstrap.Loop.NowUnixSeconds;
                Assert.IsFalse(bootstrap.Loop.Overclock.IsActive(nowAfter), "Overclock should be inactive after duration.");

                var ppsAfter = bootstrap.Loop.Production.CalculateProductionPerSecond(nowAfter);
                Assert.That(ppsAfter, Is.EqualTo(ppsBase).Within(1e-4), "PPS should return to base after Overclock ends.");

                // keep reference alive to avoid stripping warnings
                Assert.NotNull(overclock);
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

            var saveDir = Path.Combine(Application.persistentDataPath, saveSubdir);
            Directory.CreateDirectory(saveDir);
            File.WriteAllText(Path.Combine(saveDir, "save.json"), seedJson);

            var go = new GameObject("Epic04Bootstrapper");
            var bootstrap = go.AddComponent<GameBootstrapper>();
            bootstrap.BalanceConfig = balance;
            bootstrap.OverclockConfig = overclock;
            bootstrap.MapConfig = map;
            bootstrap.SetSaveLocationForTests(saveSubdir, "save");
            return bootstrap;
        }

        private static GeneratorsScreenContext CreateContext(GameBootstrapper bootstrap)
        {
            var go = new GameObject("Epic04Context");
            var ctx = go.AddComponent<GeneratorsScreenContext>();
            SetPrivateField(ctx, "_bootstrapper", bootstrap);
            return ctx;
        }

        private static HudController CreateHud(GeneratorsScreenContext context, out TMP_Text ppsText)
        {
            var canvasGo = new GameObject("Epic04Canvas", typeof(Canvas));
            canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;

            var ppsLabelGo = new GameObject("pps_label", typeof(RectTransform), typeof(TextMeshProUGUI));
            ppsLabelGo.transform.SetParent(canvasGo.transform, worldPositionStays: false);
            var ppsLabel = ppsLabelGo.GetComponent<TextMeshProUGUI>();
            ppsLabel.text = "PPS";

            var ppsNumberGo = new GameObject("pps_number", typeof(RectTransform), typeof(TextMeshProUGUI));
            ppsNumberGo.transform.SetParent(canvasGo.transform, worldPositionStays: false);
            var ppsNumber = ppsNumberGo.GetComponent<TextMeshProUGUI>();
            ppsNumber.text = "0";
            ppsText = ppsNumber;

            var hudGo = new GameObject("Epic04Hud");
            var hud = hudGo.AddComponent<HudController>();
            SetPrivateField(hud, "_context", context);
            SetPrivateField(hud, "_ppsText", ppsNumber);
            return hud;
        }

        private static GeneratorRowController CreateGeneratorRow(GeneratorsScreenContext context, int generatorId, out Button upgradeButton)
        {
            var rowGo = new GameObject($"Epic04Row_{generatorId}");
            rowGo.SetActive(false);

            var upgradeGo = new GameObject("upgrade_button", typeof(RectTransform), typeof(Image), typeof(Button));
            upgradeGo.transform.SetParent(rowGo.transform, worldPositionStays: false);
            upgradeButton = upgradeGo.GetComponent<Button>();

            var row = rowGo.AddComponent<GeneratorRowController>();
            SetPrivateField(row, "_generatorId", generatorId);
            SetPrivateField(row, "_context", context);
            SetPrivateField(row, "_upgradeButton", upgradeButton);

            rowGo.SetActive(true);
            return row;
        }

        private static OverclockButtonController CreateOverclockButton(GeneratorsScreenContext context, out Button button)
        {
            var go = new GameObject("Epic04Overclock");
            go.SetActive(false);

            var buttonGo = new GameObject("button_overclock", typeof(RectTransform), typeof(Image), typeof(Button));
            buttonGo.transform.SetParent(go.transform, worldPositionStays: false);
            button = buttonGo.GetComponent<Button>();

            var labelGo = new GameObject("label", typeof(RectTransform), typeof(TextMeshProUGUI));
            labelGo.transform.SetParent(buttonGo.transform, worldPositionStays: false);
            var label = labelGo.GetComponent<TextMeshProUGUI>();

            var ctrl = go.AddComponent<OverclockButtonController>();
            SetPrivateField(ctrl, "_context", context);
            SetPrivateField(ctrl, "_button", button);
            SetPrivateField(ctrl, "_labelText", label);

            go.SetActive(true);
            return ctrl;
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            var f = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            if (f == null) throw new MissingFieldException(target.GetType().FullName, fieldName);
            f.SetValue(target, value);
        }

        private static string SeedSaveJson(long softCurrency, int[] generatorLevels, int charges, long activeUntil, long nextChargeAt)
        {
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var levels = "[" + string.Join(",", generatorLevels) + "]";
            return "{"
                   + "\"Version\":1,"
                   + "\"SoftCurrency\":" + softCurrency + ","
                   + "\"LifetimeEarnedSoftCurrency\":0,"
                   + "\"PremiumCurrency\":0,"
                   + "\"GeneratorLevels\":" + levels + ","
                   + "\"LastLoginUnixSeconds\":" + now + ","
                   + "\"PendingOfflineGain\":0,"
                   + "\"MapSeasonId\":0,"
                   + "\"Sectors\":[],"
                   + "\"Overclock\":{\"Charges\":" + charges + ",\"ActiveUntilUnixSeconds\":" + activeUntil + ",\"NextChargeAtUnixSeconds\":" + nextChargeAt + "}"
                   + "}";
        }

        private static void TryDeleteDirectory(string dir)
        {
            try
            {
                if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
            }
            catch { /* ignore */ }
        }

        private static void DestroyAllTestObjects()
        {
            var all = UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (var i = 0; i < all.Length; i++)
            {
                var go = all[i];
                if (go == null) continue;
                if (!go.name.StartsWith("Epic04", StringComparison.Ordinal)) continue;
                UnityEngine.Object.Destroy(go);
            }
        }
    }
}
