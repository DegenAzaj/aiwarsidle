using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace AIWarsIdle.UI
{
    public enum UIRoute
    {
        Generators = 0,
        Pvp = 1,
    }

    public sealed class UIRouter : MonoBehaviour
    {
        [Serializable]
        private sealed class Tab
        {
            public Button Button;

            [Tooltip("Optional. Enabled when tab is active.")]
            public GameObject ActiveState;

            [Tooltip("Optional. Enabled when tab is inactive.")]
            public GameObject InactiveState;

            [Header("Optional Visuals")]
            [Tooltip("Optional. If set, this group alpha will be used for active/inactive fade. Assign it to a child that contains only icon/text if you don't want to fade the whole button.")]
            public CanvasGroup AlphaGroup;

            [Tooltip("Optional. Icon graphics to fade depending on active state.")]
            public Graphic[] Icons;

            [Tooltip("Optional. Text labels (TMP) to fade depending on active state.")]
            public TMP_Text[] Labels;

            [Tooltip("Alpha applied when tab is active.")]
            [Range(0f, 1f)]
            public float ActiveAlpha = 1f;

            [Tooltip("Alpha applied when tab is inactive.")]
            [Range(0f, 1f)]
            public float InactiveAlpha = 0.55f;
        }

        [Header("Lobby (always on)")]
        [Tooltip("Optional. If set, will be kept active while switching screens.")]
        [SerializeField] private GameObject _lobbyRoot;

        [Header("Screens")]
        [SerializeField] private GameObject _generatorsRoot;
        [SerializeField] private GameObject _pvpRoot;

        [Header("Tabs")]
        [SerializeField] private Tab _generatorsTab;
        [SerializeField] private Tab _pvpTab;

        [Header("Defaults")]
        [Tooltip("Used when no explicit route is requested.")]
        [SerializeField] private UIRoute _defaultRoute = UIRoute.Generators;

        [Tooltip("Optional. If enabled, EnterHub() is called when this component becomes enabled.")]
        [SerializeField] private bool _autoEnterOnEnable;

        public UIRoute CurrentRoute { get; private set; } = UIRoute.Generators;

        private Coroutine _autoEnterCoroutine;
        private Coroutine _reapplyTabVisualsCoroutine;

        public GameObject LobbyRoot { get => _lobbyRoot; set => _lobbyRoot = value; }
        public GameObject GeneratorsRoot { get => _generatorsRoot; set => _generatorsRoot = value; }
        public GameObject PvpRoot { get => _pvpRoot; set => _pvpRoot = value; }

        public void EnterHub(UIRoute? routeOverride = null)
        {
            if (_lobbyRoot != null) _lobbyRoot.SetActive(true);
            Navigate(routeOverride ?? _defaultRoute);
        }

        public void EnterHubByTab(UIRoute route)
        {
            if (_lobbyRoot != null) _lobbyRoot.SetActive(true);

            var button = route switch
            {
                UIRoute.Generators => _generatorsTab?.Button,
                UIRoute.Pvp => _pvpTab?.Button,
                _ => null,
            };

            if (button != null)
            {
                button.onClick.Invoke();
                return;
            }

            Navigate(route);
        }

        public void Navigate(UIRoute route)
        {
            CurrentRoute = route;

            SetActiveSafe(_generatorsRoot, route == UIRoute.Generators);
            SetActiveSafe(_pvpRoot, route == UIRoute.Pvp);

            ApplyTabState(_generatorsTab, isActive: route == UIRoute.Generators);
            ApplyTabState(_pvpTab, isActive: route == UIRoute.Pvp);

            // Resilient against late theme binders overwriting label/icon alpha.
            ScheduleReapplyTabVisuals();
        }

        public void HideAllScreens()
        {
            SetActiveSafe(_generatorsRoot, false);
            SetActiveSafe(_pvpRoot, false);

            ApplyTabState(_generatorsTab, isActive: false);
            ApplyTabState(_pvpTab, isActive: false);

            ScheduleReapplyTabVisuals();
        }

        private void Awake()
        {
            WireTab(_generatorsTab, UIRoute.Generators);
            WireTab(_pvpTab, UIRoute.Pvp);
        }

        private void OnEnable()
        {
            if (_autoEnterOnEnable)
            {
                if (_autoEnterCoroutine != null)
                {
                    StopCoroutine(_autoEnterCoroutine);
                }

                // Defer to next frame so UIThemeBinder.OnEnable can apply first; then we set active/inactive visuals.
                _autoEnterCoroutine = StartCoroutine(AutoEnterNextFrame());
            }
        }

        private void OnDisable()
        {
            if (_autoEnterCoroutine != null)
            {
                StopCoroutine(_autoEnterCoroutine);
                _autoEnterCoroutine = null;
            }

            if (_reapplyTabVisualsCoroutine != null)
            {
                StopCoroutine(_reapplyTabVisualsCoroutine);
                _reapplyTabVisualsCoroutine = null;
            }
        }

        private IEnumerator AutoEnterNextFrame()
        {
            yield return null;
            _autoEnterCoroutine = null;
            EnterHub();
        }

        private void WireTab(Tab tab, UIRoute route)
        {
            if (tab == null) return;
            if (tab.Button == null) return;

            tab.Button.onClick.RemoveAllListeners();
            tab.Button.onClick.AddListener(() =>
            {
                Navigate(route);
            });
        }

        private static void ApplyTabState(Tab tab, bool isActive)
        {
            if (tab == null) return;

            if (tab.Button != null)
            {
                tab.Button.interactable = !isActive;
            }

            if (tab.ActiveState != null) tab.ActiveState.SetActive(isActive);
            if (tab.InactiveState != null) tab.InactiveState.SetActive(!isActive);

            var alpha = Mathf.Clamp01(isActive ? tab.ActiveAlpha : tab.InactiveAlpha);
            if (tab.AlphaGroup != null) tab.AlphaGroup.alpha = alpha;
            ApplyAlpha(tab.Icons, alpha);
            ApplyAlpha(tab.Labels, alpha);
        }

        private static void ApplyAlpha(Graphic[] graphics, float alpha)
        {
            if (graphics == null) return;
            for (var i = 0; i < graphics.Length; i++)
            {
                var g = graphics[i];
                if (g == null) continue;
                var c = g.color;
                c.a = alpha;
                g.color = c;
            }
        }

        private static void ApplyAlpha(TMP_Text[] labels, float alpha)
        {
            if (labels == null) return;
            for (var i = 0; i < labels.Length; i++)
            {
                var t = labels[i];
                if (t == null) continue;
                var c = t.color;
                c.a = alpha;
                t.color = c;
            }
        }

        private static void SetActiveSafe(GameObject go, bool active)
        {
            if (go == null) return;
            if (go.activeSelf == active) return;
            go.SetActive(active);
        }

        private void ScheduleReapplyTabVisuals()
        {
            if (!isActiveAndEnabled) return;
            if (_reapplyTabVisualsCoroutine != null) return;
            _reapplyTabVisualsCoroutine = StartCoroutine(ReapplyTabVisualsNextFrame());
        }

        private IEnumerator ReapplyTabVisualsNextFrame()
        {
            yield return null;
            _reapplyTabVisualsCoroutine = null;

            ApplyTabState(_generatorsTab, isActive: CurrentRoute == UIRoute.Generators);
            ApplyTabState(_pvpTab, isActive: CurrentRoute == UIRoute.Pvp);
        }
    }
}
