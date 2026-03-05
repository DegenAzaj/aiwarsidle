using System.Collections;
using AIWarsIdle.UI.Splash;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AIWarsIdle.Tests
{
    public sealed class Epic11UpdateRequiredPlayModeTests
    {
        [UnityTest]
        public IEnumerator UpdateRequired_True_Shows_ForceUpdate_And_Does_Not_Enter_Hub()
        {
            var splash = new GameObject("splash_root");
            var hub = new GameObject("hub_root");
            splash.SetActive(false);
            hub.SetActive(true);

            var force = new GameObject("force_update");
            force.transform.SetParent(splash.transform, worldPositionStays: false);
            force.SetActive(false);

            var pop = new GameObject("force_update_pop");
            pop.transform.SetParent(force.transform, worldPositionStays: false);

            var downloadGo = new GameObject("download_button");
            downloadGo.transform.SetParent(pop.transform, worldPositionStays: false);
            var image = downloadGo.AddComponent<UnityEngine.UI.Image>();
            image.color = UnityEngine.Color.white;
            downloadGo.AddComponent<UnityEngine.UI.Button>();

            var controllerGo = new GameObject("ResumePolicyController");
            var controller = controllerGo.AddComponent<ResumePolicyController>();
            controller.SplashRoot = splash;
            controller.HubRoot = hub;
            controller.ShowSplashOnColdStart = true;
            controller.MinimumSplashSeconds = 0f;
            controller.SplashFadeOutSeconds = 0f;
            controller.CurrentBuildNumberProvider = () => 5;
            controller.MinAppVersionOverride = () => 6;
            controller.StoreUrlOverride = () => "https://example.invalid/store";
            string openedUrl = null;
            controller.OpenUrlProvider = url => openedUrl = url;

            yield return null; // Start + first yield
            yield return null; // evaluation + early block
            yield return null; // allow branch to activate force_update reliably

            Assert.IsTrue(splash.activeSelf, "Splash should remain active when update is required.");
            Assert.IsFalse(hub.activeSelf, "Hub should not be entered when update is required.");
            Assert.IsTrue(force.activeSelf, "force_update child should be active when update is required.");

            // Ensure button is wired and would open URL.
            var button = force.transform.Find("force_update_pop/download_button")?.GetComponent<UnityEngine.UI.Button>();
            Assert.NotNull(button, "download_button should exist for wiring.");
            Assert.IsTrue(button.interactable, "download_button should be interactable when store URL is present.");
            button.onClick.Invoke();
            Assert.AreEqual("https://example.invalid/store", openedUrl);
        }

        [UnityTest]
        public IEnumerator UpdateRequired_False_Allows_Entry_To_Hub()
        {
            var splash = new GameObject("splash_root");
            var hub = new GameObject("hub_root");
            splash.SetActive(false);
            hub.SetActive(false);

            var force = new GameObject("force_update");
            force.transform.SetParent(splash.transform, worldPositionStays: false);
            force.SetActive(false);

            var pop = new GameObject("force_update_pop");
            pop.transform.SetParent(force.transform, worldPositionStays: false);

            var downloadGo = new GameObject("download_button");
            downloadGo.transform.SetParent(pop.transform, worldPositionStays: false);
            var image = downloadGo.AddComponent<UnityEngine.UI.Image>();
            image.color = UnityEngine.Color.white;
            downloadGo.AddComponent<UnityEngine.UI.Button>();

            var controllerGo = new GameObject("ResumePolicyController");
            var controller = controllerGo.AddComponent<ResumePolicyController>();
            controller.SplashRoot = splash;
            controller.HubRoot = hub;
            controller.ShowSplashOnColdStart = true;
            controller.MinimumSplashSeconds = 0f;
            controller.SplashFadeOutSeconds = 0f;
            controller.CurrentBuildNumberProvider = () => 5;
            controller.MinAppVersionOverride = () => 5;
            controller.StoreUrlOverride = () => "https://example.invalid/store";

            yield return null;
            yield return null;
            yield return null;

            Assert.IsTrue(hub.activeSelf, "Hub should be entered when update is not required.");
            Assert.IsFalse(force.activeSelf, "force_update child should stay inactive when update is not required.");
        }
    }
}
