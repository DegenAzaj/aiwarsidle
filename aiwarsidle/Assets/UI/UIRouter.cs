using System;
using System.Collections;
using System.Collections.Generic;
using AIWarsIdle.Bootstrap;
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

        [Header("Shared HUD")]
        [Tooltip("Optional. If null, will try to find one in the scene at runtime.")]
        [SerializeField] private GameBootstrapper _bootstrapper;

        [SerializeField] private float _sharedHudRefreshIntervalSeconds = 0.25f;

        public UIRoute CurrentRoute { get; private set; } = UIRoute.Generators;

        private Coroutine _autoEnterCoroutine;
        private Coroutine _reapplyTabVisualsCoroutine;
        private readonly List<TMP_Text> _attackTexts = new();
        private readonly List<TMP_Text> _regenTexts = new();
        private float _sharedHudCarry;

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
            if (_bootstrapper == null)
            {
                _bootstrapper = FindBootstrapper();
            }

            WireTab(_generatorsTab, UIRoute.Generators);
            WireTab(_pvpTab, UIRoute.Pvp);
        }

        private void OnEnable()
        {
            _sharedHudCarry = 0f;

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

            _attackTexts.Clear();
            _regenTexts.Clear();
        }

        private void Update()
        {
            if (_sharedHudRefreshIntervalSeconds <= 0f)
            {
                RefreshSharedAttackHud();
                return;
            }

            _sharedHudCarry += Time.unscaledDeltaTime;
            if (_sharedHudCarry < _sharedHudRefreshIntervalSeconds) return;
            _sharedHudCarry = 0f;
            RefreshSharedAttackHud();
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

        private void RefreshSharedAttackHud()
        {
            EnsureSharedHudRefs();

            var loop = _bootstrapper != null ? _bootstrapper.Loop : null;
            var attacks = loop?.PvpAttacks;
            if (attacks == null)
            {
                ApplySharedHud(string.Empty, string.Empty);
                return;
            }

            var now = loop.NowUnixSeconds;
            var remaining = Mathf.Clamp(loop.State.PvpAttacksRemaining, 0, attacks.MaxAttacks);
            var max = attacks.MaxAttacks;
            var nextRegenAt = loop.State.NextPvpAttackRegenAtUnixSeconds;

            var countText = $"{remaining}/{Mathf.Max(max, remaining)}";
            var regenText = max > 0 && remaining < max && nextRegenAt > now
                ? FormatClockDuration(nextRegenAt - now)
                : string.Empty;

            ApplySharedHud(countText, regenText);
        }

        private void ApplySharedHud(string countText, string regenText)
        {
            for (var i = 0; i < _attackTexts.Count; i++)
            {
                if (_attackTexts[i] != null) _attackTexts[i].text = countText;
            }

            for (var i = 0; i < _regenTexts.Count; i++)
            {
                if (_regenTexts[i] != null) _regenTexts[i].text = regenText;
            }
        }

        private void EnsureSharedHudRefs()
        {
            if (_attackTexts.Count > 0 && _regenTexts.Count > 0) return;

            var attacksBarRoot = FindNamedChildInScene("res_bar");
            if (_attackTexts.Count == 0 && attacksBarRoot != null)
            {
                var energyBar = FindNamedChildRecursive(attacksBarRoot, "ResourceBar_Energy");
                CollectTextComponents(
                    energyBar,
                    _attackTexts,
                    text => text.name == "Text_Energy" || text.name == "Text (TMP)");
            }

            var timerRoot = FindNamedChildInScene("res_bar_2");
            if (_regenTexts.Count == 0 && timerRoot != null)
            {
                var regenRoot = FindNamedChildRecursive(timerRoot, "energy_regen");
                CollectTextComponents(
                    regenRoot,
                    _regenTexts,
                    text => text.name == "Text (TMP)");
            }
        }

        private Transform FindNamedChildInScene(string name)
        {
            var scene = gameObject.scene;
            if (!scene.IsValid() || !scene.isLoaded) return null;

            var roots = scene.GetRootGameObjects();
            for (var i = 0; i < roots.Length; i++)
            {
                var found = FindNamedChildRecursive(roots[i].transform, name);
                if (found != null) return found;
            }

            return null;
        }

        private static Transform FindNamedChildRecursive(Transform root, string name)
        {
            if (root == null) return null;
            if (root.name == name) return root;

            for (var i = 0; i < root.childCount; i++)
            {
                var found = FindNamedChildRecursive(root.GetChild(i), name);
                if (found != null) return found;
            }

            return null;
        }

        private static void CollectTextComponents(Transform root, List<TMP_Text> target, Predicate<TMP_Text> predicate)
        {
            if (root == null || target == null) return;

            var matches = new List<TMP_Text>();
            CollectTextComponentsRecursive(root, matches, predicate);
            if (matches.Count == 0) return;

            var hasActiveMatch = false;
            for (var i = 0; i < matches.Count; i++)
            {
                if (matches[i] != null && matches[i].gameObject.activeInHierarchy)
                {
                    hasActiveMatch = true;
                    break;
                }
            }

            for (var i = 0; i < matches.Count; i++)
            {
                var text = matches[i];
                if (text == null) continue;
                if (hasActiveMatch && !text.gameObject.activeInHierarchy) continue;
                target.Add(text);
            }
        }

        private static void CollectTextComponentsRecursive(Transform root, List<TMP_Text> target, Predicate<TMP_Text> predicate)
        {
            if (root == null) return;

            if (root.TryGetComponent<TMP_Text>(out var text) && (predicate == null || predicate(text)))
            {
                target.Add(text);
            }

            for (var i = 0; i < root.childCount; i++)
            {
                CollectTextComponentsRecursive(root.GetChild(i), target, predicate);
            }
        }

        private static string FormatClockDuration(long seconds)
        {
            if (seconds <= 0) return "0:00:00";

            var ts = TimeSpan.FromSeconds(seconds);
            return $"{Math.Max(0, (int)ts.TotalHours)}:{ts.Minutes:D2}:{ts.Seconds:D2}";
        }

        private static GameBootstrapper FindBootstrapper()
        {
#if UNITY_2023_1_OR_NEWER
            var found = UnityEngine.Object.FindFirstObjectByType<GameBootstrapper>(FindObjectsInactive.Exclude);
            if (found != null) return found;
            return UnityEngine.Object.FindAnyObjectByType<GameBootstrapper>(FindObjectsInactive.Exclude);
#else
            return UnityEngine.Object.FindObjectOfType<GameBootstrapper>();
#endif
        }
    }
}
