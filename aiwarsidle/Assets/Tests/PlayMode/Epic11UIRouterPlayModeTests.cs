using System.Collections;
using AIWarsIdle.UI;
using AIWarsIdle.UI.Splash;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AIWarsIdle.Tests
{
    public sealed class Epic11UIRouterPlayModeTests
    {
        private sealed class ScreenStateProbe : MonoBehaviour
        {
            public int StartCalls { get; private set; }
            public int Value;

            private void Start()
            {
                StartCalls++;
            }
        }

        [UnityTest]
        public IEnumerator UIRouter_Navigate_Toggles_Pvp_And_Generators_Roots_And_Keeps_Lobby_Active()
        {
            var hub = new GameObject("hub_root");
            var lobby = new GameObject("lobby_root");
            lobby.transform.SetParent(hub.transform, worldPositionStays: false);
            lobby.SetActive(true);

            var generators = new GameObject("generators_root");
            var pvp = new GameObject("pvp_root");
            generators.SetActive(false);
            pvp.SetActive(false);

            var router = hub.AddComponent<UIRouter>();
            router.LobbyRoot = lobby;
            router.GeneratorsRoot = generators;
            router.PvpRoot = pvp;

            router.Navigate(UIRoute.Pvp);
            Assert.IsTrue(lobby.activeSelf, "Lobby should stay active while switching screens.");
            Assert.IsTrue(pvp.activeSelf, "PvP root should be active when routed to PvP.");
            Assert.IsFalse(generators.activeSelf, "Generators root should be inactive when routed to PvP.");

            router.Navigate(UIRoute.Generators);
            Assert.IsTrue(lobby.activeSelf, "Lobby should stay active while switching screens.");
            Assert.IsTrue(generators.activeSelf, "Generators root should be active when routed to Generators.");
            Assert.IsFalse(pvp.activeSelf, "PvP root should be inactive when routed to Generators.");

            yield break;
        }

        [UnityTest]
        public IEnumerator Switching_Screens_Does_Not_Lose_State()
        {
            LogAssert.NoUnexpectedReceived();

            var hub = new GameObject("hub_root");
            var lobby = new GameObject("lobby_root");
            lobby.transform.SetParent(hub.transform, worldPositionStays: false);
            lobby.SetActive(true);

            var generators = new GameObject("generators_root");
            var pvp = new GameObject("pvp_root");
            generators.SetActive(false);
            pvp.SetActive(false);

            var pvpProbe = pvp.AddComponent<ScreenStateProbe>();
            var generatorsProbe = generators.AddComponent<ScreenStateProbe>();

            var router = hub.AddComponent<UIRouter>();
            router.LobbyRoot = lobby;
            router.GeneratorsRoot = generators;
            router.PvpRoot = pvp;

            router.Navigate(UIRoute.Pvp);
            yield return null; // allow Start on activated screen

            pvpProbe.Value = 123;
            Assert.AreEqual(1, pvpProbe.StartCalls, "PvP screen Start should run exactly once after first activation.");

            router.Navigate(UIRoute.Generators);
            yield return null; // allow Start on activated screen
            Assert.AreEqual(1, generatorsProbe.StartCalls, "Generators screen Start should run exactly once after first activation.");

            router.Navigate(UIRoute.Pvp);
            yield return null; // allow re-activation

            Assert.AreSame(pvpProbe, pvp.GetComponent<ScreenStateProbe>(), "PvP screen component should not be recreated.");
            Assert.AreEqual(123, pvpProbe.Value, "PvP screen state should persist across deactivation/reactivation.");
            Assert.AreEqual(1, pvpProbe.StartCalls, "PvP screen Start should not run again on re-activation.");

            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator Splash_Entry_Routes_To_Pvp_Via_UIRouter()
        {
            var splash = new GameObject("splash_root");
            var hub = new GameObject("hub_root");
            splash.SetActive(false);
            hub.SetActive(false);

            var lobby = new GameObject("lobby_root");
            lobby.transform.SetParent(hub.transform, worldPositionStays: false);
            lobby.SetActive(true);

            var generators = new GameObject("generators_root");
            var pvp = new GameObject("pvp_root");
            generators.SetActive(false);
            pvp.SetActive(false);

            var router = hub.AddComponent<UIRouter>();
            router.LobbyRoot = lobby;
            router.GeneratorsRoot = generators;
            router.PvpRoot = pvp;

            var controllerGo = new GameObject("ResumePolicyController");
            var controller = controllerGo.AddComponent<ResumePolicyController>();
            controller.SplashRoot = splash;
            controller.HubRoot = hub;
            controller.ShowSplashOnColdStart = true;
            controller.MinimumSplashSeconds = 0f;
            controller.SplashFadeOutSeconds = 0f;
            controller.CurrentBuildNumberProvider = () => 1;
            controller.MinAppVersionOverride = () => 0;

            yield return null; // Start + first yield
            yield return null; // routing continues
            yield return null; // allow hub routing to apply

            Assert.IsFalse(hub.activeSelf, "HubRoot should remain inactive; routing is performed via UIRouter.");
            Assert.IsTrue(pvp.activeSelf, "PvP should be the entry screen routed via UIRouter.");
            Assert.IsFalse(generators.activeSelf, "Generators should not be active on Splash entry when routing to PvP.");
        }
    }
}
