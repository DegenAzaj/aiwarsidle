using System.Collections;
using AIWarsIdle.UI.Splash;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AIWarsIdle.Tests
{
    public sealed class Epic11ResumePolicyPlayModeTests
    {
        [UnityTest]
        public IEnumerator Resume_After_Timeout_Shows_Splash_With_TouchContinue_And_Waits_For_Input_Then_Goes_To_Hub()
        {
            var splash = new GameObject("splash_root");
            var hub = new GameObject("hub_root");
            splash.SetActive(false);
            hub.SetActive(true);

            var touch = new GameObject("touch_continue_dim");
            touch.transform.SetParent(splash.transform, worldPositionStays: false);
            touch.SetActive(false);

            var controllerGo = new GameObject("ResumePolicyController");
            var controller = controllerGo.AddComponent<ResumePolicyController>();

            controller.SplashRoot = splash;
            controller.HubRoot = hub;
            controller.TouchContinueDim = touch;
            controller.ShowSplashOnColdStart = false;
            controller.MinimumSplashSeconds = 0f;
            controller.SplashFadeOutSeconds = 0f;
            controller.ResumeToSplashThresholdMinutes = 10f;

            var now = 1000L;
            controller.NowUnixSecondsUtcProvider = () => now;
            controller.ContinuePressedThisFrameProvider = () => false;

            yield return null; // allow Start() to run

            controller.SendMessage("OnApplicationPause", true);
            now += 600; // >= 10 min
            controller.SendMessage("OnApplicationPause", false);

            yield return null; // let routing coroutine start + first yield

            Assert.IsTrue(splash.activeSelf, "Splash should be active after inactivity timeout.");
            Assert.IsFalse(hub.activeSelf, "Hub should be inactive while waiting on Splash.");
            Assert.IsTrue(touch.activeSelf, "touch_continue_dim should be active in touch-to-continue mode.");

            controller.ContinuePressedThisFrameProvider = () => true;
            yield return null; // allow continue + transition

            Assert.IsFalse(splash.activeSelf, "Splash should be inactive after user continues.");
            Assert.IsTrue(hub.activeSelf, "Hub should be active after user continues.");
            Assert.IsFalse(touch.activeSelf, "touch_continue_dim should be disabled after leaving Splash.");
        }

        [UnityTest]
        public IEnumerator Resume_Before_Timeout_Does_Not_Show_Splash_Or_Reset_Routing()
        {
            var splash = new GameObject("splash_root");
            var hub = new GameObject("hub_root");
            splash.SetActive(false);
            hub.SetActive(true);

            var touch = new GameObject("touch_continue_dim");
            touch.transform.SetParent(splash.transform, worldPositionStays: false);
            touch.SetActive(false);

            var controllerGo = new GameObject("ResumePolicyController");
            var controller = controllerGo.AddComponent<ResumePolicyController>();

            controller.SplashRoot = splash;
            controller.HubRoot = hub;
            controller.TouchContinueDim = touch;
            controller.ShowSplashOnColdStart = false;
            controller.MinimumSplashSeconds = 0f;
            controller.SplashFadeOutSeconds = 0f;
            controller.ResumeToSplashThresholdMinutes = 10f;

            var now = 2000L;
            controller.NowUnixSecondsUtcProvider = () => now;

            yield return null;

            controller.SendMessage("OnApplicationPause", true);
            now += 599; // < 10 min
            controller.SendMessage("OnApplicationPause", false);

            yield return null;

            Assert.IsFalse(splash.activeSelf, "Splash should remain inactive when resuming before timeout.");
            Assert.IsTrue(hub.activeSelf, "Hub should remain active when resuming before timeout.");
            Assert.IsFalse(touch.activeSelf, "touch_continue_dim should remain inactive when Splash is not shown.");
        }
    }
}

