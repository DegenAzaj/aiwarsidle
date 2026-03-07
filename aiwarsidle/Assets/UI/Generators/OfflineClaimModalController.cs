using System;
using AIWarsIdle.Bootstrap;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AIWarsIdle.UI.Generators
{
    public sealed class OfflineClaimModalController : MonoBehaviour
    {
        [Header("Context")]
        [SerializeField] private GameBootstrapper _bootstrapper;

        [Header("Roots")]
        [SerializeField] private GameObject _modalRoot;
        [SerializeField] private GameObject _splashRoot;

        [Header("Buttons")]
        [SerializeField] private Button _hudClaimButton;
        [SerializeField] private Button _claimButton;
        [SerializeField] private Button _claimX2Button;
        [SerializeField] private Button _notNowButton;

        [Header("Texts")]
        [SerializeField] private TMP_Text _offlineGainNumberText;
        [SerializeField] private TMP_Text _offlineTimeDescriptionText;

        [Header("Refresh")]
        [SerializeField] private float _refreshIntervalSeconds = 0.1f;

        private float _refreshCarry;
        private bool _dismissedForCurrentEntry;
        private bool _wasSplashVisible;
        private bool _isVisible;

        private void Awake()
        {
            if (_bootstrapper == null) _bootstrapper = GetComponent<GameBootstrapper>();
            if (_bootstrapper == null)
            {
                _bootstrapper = FindBootstrapper();
            }

            if (_modalRoot == null)
            {
                _modalRoot = FindSceneObject("claim_offline_root");
            }

            ResolveUiReferences();
            SetVisible(false);
            _wasSplashVisible = IsSplashVisible();
        }

        private void OnEnable()
        {
            BindButtons();
            TickVisibility();
            RefreshVisuals();
        }

        private void OnDisable()
        {
            UnbindButtons();
        }

        private void Update()
        {
            TickVisibility();

            if (_refreshIntervalSeconds > 0f)
            {
                _refreshCarry += Time.unscaledDeltaTime;
                if (_refreshCarry < _refreshIntervalSeconds) return;
                _refreshCarry = 0f;
            }
            RefreshVisuals();
        }

        private void TickVisibility()
        {
            ResolveUiReferences();

            var splashVisible = IsSplashVisible();
            if (splashVisible)
            {
                UpdateHudClaimButton(canClaim: false);
                _wasSplashVisible = true;
                _dismissedForCurrentEntry = false;
                if (_isVisible) SetVisible(false);
                return;
            }

            if (_wasSplashVisible)
            {
                _wasSplashVisible = false;
                _dismissedForCurrentEntry = false;
            }

            if (_dismissedForCurrentEntry)
            {
                var loopWhileDismissed = ResolveLoop();
                var canClaimWhileDismissed = loopWhileDismissed != null && loopWhileDismissed.OfflineClaim.PendingOfflineGain > 0;
                UpdateHudClaimButton(canClaimWhileDismissed);
                if (_isVisible) SetVisible(false);
                return;
            }

            var loop = ResolveLoop();
            if (loop == null)
            {
                UpdateHudClaimButton(canClaim: false);
                if (_isVisible) SetVisible(false);
                return;
            }

            var shouldShow = loop.OfflineClaim.PendingOfflineGain > 0;
            UpdateHudClaimButton(shouldShow);
            SetVisible(shouldShow);
        }

        private void RefreshVisuals()
        {
            var loop = ResolveLoop();
            if (loop == null)
            {
                if (_offlineGainNumberText != null) _offlineGainNumberText.text = UiFormat.Compact(0);
                if (_offlineTimeDescriptionText != null)
                {
                    _offlineTimeDescriptionText.text = $"Offline Time: {UiFormat.Duration(0)} (CAP: {FormatCap(loop: null)})";
                }
                return;
            }

            if (_offlineGainNumberText != null)
            {
                _offlineGainNumberText.text = UiFormat.Compact(loop.OfflineClaim.PendingOfflineGain);
            }

            if (_offlineTimeDescriptionText != null)
            {
                var rawSeconds = ResolveDisplayedOfflineSeconds(loop);
                _offlineTimeDescriptionText.text = $"Offline Time: {UiFormat.Duration(rawSeconds)} (CAP: {FormatCap(loop)})";
            }

            var canClaim = loop.OfflineClaim.PendingOfflineGain > 0;
            UpdateHudClaimButton(canClaim);
            if (_claimButton != null) _claimButton.interactable = canClaim;
            if (_claimX2Button != null) _claimX2Button.interactable = canClaim && loop.RewardedAds != null;
        }

        private void OnClaimClicked()
        {
            var loop = ResolveLoop();
            if (loop == null) return;
            if (loop.OfflineClaim.PendingOfflineGain <= 0)
            {
                SetVisible(false);
                RefreshVisuals();
                return;
            }

            loop.OfflineClaim.Claim(multiplier: 1);
            loop.ForceSave();
            _dismissedForCurrentEntry = true;
            SetVisible(false);
            RefreshVisuals();
        }

        private void OnClaimX2Clicked()
        {
            var loop = ResolveLoop();
            if (loop == null) return;
            if (loop.OfflineClaim.PendingOfflineGain <= 0)
            {
                SetVisible(false);
                RefreshVisuals();
                return;
            }

            var shown = loop.RewardedAds != null && loop.RewardedAds.TryShowOfflineClaimX2(loop.NowUnixSeconds);
            if (!shown)
            {
                RefreshVisuals();
                return;
            }

            if (loop.OfflineClaim.PendingOfflineGain <= 0)
            {
                loop.ForceSave();
                _dismissedForCurrentEntry = true;
                SetVisible(false);
                RefreshVisuals();
                return;
            }

            RefreshVisuals();
        }

        private void OnNotNowClicked()
        {
            _dismissedForCurrentEntry = true;
            SetVisible(false);
            RefreshVisuals();
        }

        private void OnHudClaimClicked()
        {
            var loop = ResolveLoop();
            if (loop == null) return;
            if (loop.OfflineClaim.PendingOfflineGain <= 0)
            {
                RefreshVisuals();
                return;
            }

            if (IsSplashVisible()) return;

            _dismissedForCurrentEntry = false;
            SetVisible(true);
            RefreshVisuals();
        }

        private void BindButtons()
        {
            UnbindButtons();
            if (_hudClaimButton != null) _hudClaimButton.onClick.AddListener(OnHudClaimClicked);
            if (_claimButton != null) _claimButton.onClick.AddListener(OnClaimClicked);
            if (_claimX2Button != null) _claimX2Button.onClick.AddListener(OnClaimX2Clicked);
            if (_notNowButton != null) _notNowButton.onClick.AddListener(OnNotNowClicked);
        }

        private void UnbindButtons()
        {
            if (_hudClaimButton != null) _hudClaimButton.onClick.RemoveListener(OnHudClaimClicked);
            if (_claimButton != null) _claimButton.onClick.RemoveListener(OnClaimClicked);
            if (_claimX2Button != null) _claimX2Button.onClick.RemoveListener(OnClaimX2Clicked);
            if (_notNowButton != null) _notNowButton.onClick.RemoveListener(OnNotNowClicked);
        }

        private void ResolveUiReferences()
        {
            if (_hudClaimButton == null) _hudClaimButton = FindSceneComponent<Button>("button_claim_offline");
            if (_modalRoot == null) return;

            if (_claimButton == null) _claimButton = FindComponentInChildren<Button>(_modalRoot, "button_claim");
            if (_claimX2Button == null) _claimX2Button = FindComponentInChildren<Button>(_modalRoot, "button_claim_x2");
            if (_notNowButton == null) _notNowButton = FindComponentInChildren<Button>(_modalRoot, "button_no_thanks");
            if (_offlineGainNumberText == null) _offlineGainNumberText = FindComponentInChildren<TMP_Text>(_modalRoot, "offline_gain_number");
            if (_offlineTimeDescriptionText == null) _offlineTimeDescriptionText = FindComponentInChildren<TMP_Text>(_modalRoot, "offline_time_description");
        }

        private GameLoop ResolveLoop()
        {
            if (_bootstrapper == null)
            {
                _bootstrapper = FindBootstrapper();
            }

            if (_bootstrapper == null) return null;
            _bootstrapper.Initialize();
            return _bootstrapper.Loop;
        }

        private bool IsSplashVisible()
        {
            if (_splashRoot == null)
            {
                _splashRoot = FindSceneObject("SplashScreen");
            }

            return _splashRoot != null && _splashRoot.activeInHierarchy;
        }

        private static string FormatCap(GameLoop loop)
        {
            var capSeconds = loop != null
                ? Mathf.Max(0, Mathf.RoundToInt((float)loop.OfflineClaim.OfflineCapSeconds))
                : 0;

            if (capSeconds <= 0) return "0s";
            if (capSeconds % 3600 == 0) return $"{capSeconds / 3600}h";
            return UiFormat.Duration(capSeconds);
        }

        private static long ResolveDisplayedOfflineSeconds(GameLoop loop)
        {
            var rawSeconds = loop.OfflineClaim.LastBankedOfflineRawSeconds;
            if (rawSeconds > 0) return rawSeconds;

            var effectiveSeconds = loop.OfflineClaim.LastBankedOfflineEffectiveSeconds;
            if (effectiveSeconds > 0) return effectiveSeconds;

            var pps = loop.Production.CalculateProductionPerSecond(loop.NowUnixSeconds);
            if (pps <= 0) return 0;

            var estimatedSeconds = (long)Math.Round(loop.OfflineClaim.PendingOfflineGain / pps);
            return estimatedSeconds < 0 ? 0 : estimatedSeconds;
        }

        private void SetVisible(bool visible)
        {
            _isVisible = visible;
            if (_modalRoot != null && _modalRoot.activeSelf != visible)
            {
                _modalRoot.SetActive(visible);
            }
        }

        private void UpdateHudClaimButton(bool canClaim)
        {
            if (_hudClaimButton == null) return;
            _hudClaimButton.interactable = canClaim;
        }

        private static T FindComponentInChildren<T>(GameObject root, string childName) where T : Component
        {
            var child = FindChildByName(root, childName);
            return child != null ? child.GetComponent<T>() : null;
        }

        private static T FindSceneComponent<T>(string objectName) where T : Component
        {
            var sceneObject = FindSceneObject(objectName);
            return sceneObject != null ? sceneObject.GetComponent<T>() : null;
        }

        private static GameObject FindChildByName(GameObject root, string childName)
        {
            if (root == null || string.IsNullOrWhiteSpace(childName)) return null;

            var transforms = root.GetComponentsInChildren<Transform>(includeInactive: true);
            for (var i = 0; i < transforms.Length; i++)
            {
                var t = transforms[i];
                if (t == null) continue;
                if (string.Equals(t.name, childName, StringComparison.Ordinal)) return t.gameObject;
            }

            return null;
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

        private static GameObject FindSceneObject(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;

            var allObjects = Resources.FindObjectsOfTypeAll<GameObject>();
            for (var i = 0; i < allObjects.Length; i++)
            {
                var go = allObjects[i];
                if (go == null) continue;
                if (string.IsNullOrEmpty(go.scene.name)) continue;
                if (!string.Equals(go.name, name, StringComparison.Ordinal)) continue;
                return go;
            }

            return null;
        }
    }
}
