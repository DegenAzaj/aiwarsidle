using System;
using System.Collections;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
using AIWarsIdle.UI;

namespace AIWarsIdle.UI.Splash
{
    public sealed class ResumePolicyController : MonoBehaviour
    {
        public Func<long> NowUnixSecondsUtcProvider { get; set; }
        public Func<bool> ContinuePressedThisFrameProvider { get; set; }
        public Func<long?> MinAppVersionOverride { get; set; }
        public Func<long> CurrentBuildNumberProvider { get; set; }
        public Func<string> StoreUrlOverride { get; set; }
        public Action<string> OpenUrlProvider { get; set; }

        [Header("Roots")]
        [SerializeField] private GameObject _splashRoot;
        [SerializeField] private GameObject _hubRoot;

        [Header("Hub Routing")]
        [Tooltip("Optional. If null, will try to find UIRouter under HubRoot.")]
        [SerializeField] private UIRouter _hubRouter;

        [Tooltip("Route requested when entering Hub via Splash.")]
        [SerializeField] private UIRoute _hubEntryRoute = UIRoute.Pvp;

        [Header("Splash FX")]
        [Tooltip("Optional. If null, a CanvasGroup will be searched on SplashRoot (or added at runtime).")]
        [SerializeField] private CanvasGroup _splashCanvasGroup;

        [Tooltip("Fade-out duration when leaving Splash (seconds). Set 0 for instant switch.")]
        [SerializeField] private float _splashFadeOutSeconds = 0.25f;

        [Header("Splash UI")]
        [Tooltip("Optional. If null, will look for a child named 'touch_continue_dim' under SplashRoot.")]
        [SerializeField] private GameObject _touchContinueDim;

        [Tooltip("Optional. If null, will look for a child named 'force_update' under SplashRoot.")]
        [SerializeField] private GameObject _forceUpdate;

        [Header("Remote Config")]
        [Tooltip("Remote Config key which specifies minimum allowed app build number.")]
        [SerializeField] private string _minAppVersionKey = "min_app_version";

        [Tooltip("Remote Config key with Store URL for force update flow.")]
        [SerializeField] private string _storeUrlKey = "store_url";

        [Tooltip("How long to wait for Firebase Remote Config before allowing entry (seconds). 0 = don't wait.")]
        [SerializeField] private float _remoteConfigTimeoutSeconds = 5f;

        [Header("Force Update UI")]
        [Tooltip("Path under 'force_update' root to the download button.")]
        [SerializeField] private string _downloadButtonPath = "force_update_pop/download_button";

        [Header("Policy")]
        [Tooltip("If app resumes after >= this threshold, show Splash and route to Hub.")]
        [SerializeField] private float _resumeToSplashThresholdMinutes = 10f;

        [Tooltip("Optional: keep Splash visible for at least this many seconds (useful for layout / async checks).")]
        [SerializeField] private float _minimumSplashSeconds = 0f;

        [Tooltip("On cold start, show Splash and route to Hub.")]
        [SerializeField] private bool _showSplashOnColdStart = true;

        private long _lastPausedAtUnixSecondsUtc;
        private Coroutine _routingCoroutine;
        private bool _showTouchContinueDim;
        private bool _isUpdateRequired;
        private string _storeUrl;
        private UnityEngine.UI.Button _downloadButton;

        public GameObject SplashRoot { get => _splashRoot; set => _splashRoot = value; }
        public GameObject HubRoot
        {
            get => _hubRoot;
            set
            {
                _hubRoot = value;
                SetActiveSafe(_hubRoot, false);
            }
        }
        public GameObject TouchContinueDim { get => _touchContinueDim; set => _touchContinueDim = value; }
        public float ResumeToSplashThresholdMinutes { get => _resumeToSplashThresholdMinutes; set => _resumeToSplashThresholdMinutes = value; }
        public float MinimumSplashSeconds { get => _minimumSplashSeconds; set => _minimumSplashSeconds = value; }
        public float SplashFadeOutSeconds { get => _splashFadeOutSeconds; set => _splashFadeOutSeconds = value; }
        public bool ShowSplashOnColdStart { get => _showSplashOnColdStart; set => _showSplashOnColdStart = value; }

        private void Awake()
        {
            // HubRoot is treated as a legacy container; it must remain inactive.
            SetActiveSafe(_hubRoot, false);
        }

        private void Start()
        {
            if (_showSplashOnColdStart)
            {
                RouteToHubThroughSplash(showTouchContinueDim: false);
            }
        }

        private void OnApplicationPause(bool isPaused)
        {
            if (isPaused)
            {
                _lastPausedAtUnixSecondsUtc = NowUnixSecondsUtc();
                return;
            }

            if (_resumeToSplashThresholdMinutes <= 0f) return;
            if (_lastPausedAtUnixSecondsUtc <= 0) return;

            var now = NowUnixSecondsUtc();
            var ageSeconds = now - _lastPausedAtUnixSecondsUtc;
            if (ageSeconds < 0) ageSeconds = 0;

            var thresholdSeconds = (long)Math.Ceiling(_resumeToSplashThresholdMinutes * 60f);
            if (ageSeconds >= thresholdSeconds)
            {
                RouteToHubThroughSplash(showTouchContinueDim: true);
            }
        }

        public void RouteToHubThroughSplash(bool showTouchContinueDim = false)
        {
            if (_splashRoot == null || _hubRoot == null)
            {
                Debug.LogWarning($"{nameof(ResumePolicyController)}: Missing references. Assign SplashRoot and HubRoot.");
                return;
            }

            _showTouchContinueDim = showTouchContinueDim;

            // Apply initial state immediately to avoid 1-frame Hub flash (HubRoot might be active in scene).
            HideHubScreensIfPossible();
            SetActiveSafe(_hubRoot, false);
            SetActiveSafe(_splashRoot, true);

            if (_routingCoroutine != null)
            {
                StopCoroutine(_routingCoroutine);
            }

            _routingCoroutine = StartCoroutine(RouteRoutine());
        }

        public void DebugTriggerInactivityTimeout()
        {
            RouteToHubThroughSplash(showTouchContinueDim: true);
        }

        private IEnumerator RouteRoutine()
        {
            HideHubScreensIfPossible();
            SetActiveSafe(_hubRoot, false);
            SetActiveSafe(_splashRoot, true);

            var splashCanvasGroup = ResolveSplashCanvasGroup();
            if (splashCanvasGroup != null)
            {
                splashCanvasGroup.alpha = 1f;
                splashCanvasGroup.interactable = true;
                splashCanvasGroup.blocksRaycasts = true;
            }

            var touchContinue = ResolveTouchContinueDim();
            if (touchContinue != null)
            {
                SetActiveSafe(touchContinue, _showTouchContinueDim);
            }

            var forceUpdate = ResolveForceUpdate();
            if (forceUpdate != null)
            {
                SetActiveSafe(forceUpdate, false);
            }

            if (_minimumSplashSeconds > 0f)
            {
                yield return new WaitForSeconds(_minimumSplashSeconds);
            }
            else
            {
                yield return null;
            }

            yield return EvaluateUpdateRequired();
            if (_isUpdateRequired)
            {
                if (touchContinue != null) SetActiveSafe(touchContinue, false);
                forceUpdate ??= ResolveForceUpdate();
                if (forceUpdate != null) SetActiveSafe(forceUpdate, true);
                SetActiveSafe(_hubRoot, false);
                WireForceUpdateDownloadButton(forceUpdate);

                if (Debug.isDebugBuild)
                {
                    Debug.Log($"[Splash] force_update_active={(forceUpdate != null && forceUpdate.activeSelf)} splash_active={(_splashRoot != null && _splashRoot.activeSelf)} hub_active={(_hubRoot != null && _hubRoot.activeSelf)}");
                }
                _routingCoroutine = null;
                yield break;
            }

            if (_showTouchContinueDim)
            {
                while (!WasContinuePressedThisFrame())
                {
                    yield return null;
                }
            }

            if (_splashFadeOutSeconds > 0f && splashCanvasGroup != null)
            {
                RouteHubIfPossible();
                yield return FadeCanvasGroup(splashCanvasGroup, from: 1f, to: 0f, _splashFadeOutSeconds);
                splashCanvasGroup.interactable = false;
                splashCanvasGroup.blocksRaycasts = false;
            }
            else
            {
                RouteHubIfPossible();
            }

            if (touchContinue != null)
            {
                SetActiveSafe(touchContinue, false);
            }

            SetActiveSafe(_splashRoot, false);

            _routingCoroutine = null;
        }

        private void HideHubScreensIfPossible()
        {
            try
            {
                var router = _hubRouter;
                if (router == null && _hubRoot != null)
                {
                    router = _hubRoot.GetComponentInChildren<UIRouter>(includeInactive: true);
                }

                router?.HideAllScreens();
            }
            catch
            {
                // Best-effort only.
            }
        }

        private void RouteHubIfPossible()
        {
            try
            {
                var router = _hubRouter;
                if (router == null && _hubRoot != null)
                {
                    router = _hubRoot.GetComponentInChildren<UIRouter>(includeInactive: true);
                }

                router?.EnterHubByTab(_hubEntryRoute);
            }
            catch
            {
                // Never block entry due to UI routing errors.
            }
        }

        private static void SetActiveSafe(GameObject go, bool active)
        {
            if (go == null) return;
            if (go.activeSelf == active) return;
            go.SetActive(active);
        }

        private long NowUnixSecondsUtc()
        {
            return NowUnixSecondsUtcProvider != null ? NowUnixSecondsUtcProvider() : DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        }

        private CanvasGroup ResolveSplashCanvasGroup()
        {
            if (_splashCanvasGroup != null) return _splashCanvasGroup;
            if (_splashRoot == null) return null;

            var cg = _splashRoot.GetComponent<CanvasGroup>();
            if (cg == null)
            {
                cg = _splashRoot.AddComponent<CanvasGroup>();
            }

            _splashCanvasGroup = cg;
            return cg;
        }

        private GameObject ResolveTouchContinueDim()
        {
            if (_touchContinueDim != null) return _touchContinueDim;
            if (_splashRoot == null) return null;

            var t = _splashRoot.transform.Find("touch_continue_dim");
            if (t == null) return null;

            _touchContinueDim = t.gameObject;
            return _touchContinueDim;
        }

        private GameObject ResolveForceUpdate()
        {
            if (_forceUpdate != null) return _forceUpdate;
            if (_splashRoot == null) return null;

            var t = _splashRoot.transform.Find("force_update");
            if (t == null) return null;

            _forceUpdate = t.gameObject;
            return _forceUpdate;
        }

        private IEnumerator EvaluateUpdateRequired()
        {
            if (TryEvaluateUpdateRequiredImmediate(out var minAppVersion, out var currentBuild))
            {
                LogUpdateGateDecision(currentBuild, minAppVersion);
                yield break;
            }

            // Remote Config path (async)
            _isUpdateRequired = false;
            _storeUrl = null;

            currentBuild = GetCurrentBuildNumber();
            if (_remoteConfigTimeoutSeconds <= 0f)
            {
                LogUpdateGateDecision(currentBuild, minAppVersion: null);
                yield break;
            }

            yield return FirebaseRemoteConfigMinAppVersion.FetchAndGetLong(_minAppVersionKey, _remoteConfigTimeoutSeconds, r => minAppVersion = r);

            if (minAppVersion.HasValue && minAppVersion.Value > 0 && currentBuild > 0)
            {
                _isUpdateRequired = currentBuild < minAppVersion.Value;
                if (_isUpdateRequired)
                {
                    _storeUrl = ResolveStoreUrlOverride();
                    if (string.IsNullOrWhiteSpace(_storeUrl))
                    {
                        yield return FirebaseRemoteConfigStringValue.FetchAndGetString(_storeUrlKey, _remoteConfigTimeoutSeconds, r => _storeUrl = r);
                    }
                }
            }

            LogUpdateGateDecision(currentBuild, minAppVersion);
        }

        private long GetCurrentBuildNumber()
        {
            if (CurrentBuildNumberProvider != null) return CurrentBuildNumberProvider();
            return AppBuildNumber.GetCurrent();
        }

        private bool TryEvaluateUpdateRequiredImmediate(out long? minAppVersion, out long currentBuild)
        {
            minAppVersion = null;
            currentBuild = GetCurrentBuildNumber();

            if (MinAppVersionOverride == null) return false;

            _isUpdateRequired = false;
            _storeUrl = null;

            minAppVersion = MinAppVersionOverride();

            if (minAppVersion.HasValue && minAppVersion.Value > 0 && currentBuild > 0)
            {
                _isUpdateRequired = currentBuild < minAppVersion.Value;
            }

            if (_isUpdateRequired)
            {
                _storeUrl = ResolveStoreUrlOverride();
            }

            return true;
        }

        private void LogUpdateGateDecision(long currentBuild, long? minAppVersion)
        {
            if (!Debug.isDebugBuild) return;

            var minStr = minAppVersion.HasValue ? minAppVersion.Value.ToString() : "<null>";
            Debug.Log($"[Splash] build={currentBuild} min_app_version={minStr} update_required={_isUpdateRequired} store_url={(string.IsNullOrWhiteSpace(_storeUrl) ? "<empty>" : _storeUrl)}");
        }

        private string ResolveStoreUrlOverride()
        {
            try
            {
                return StoreUrlOverride != null ? StoreUrlOverride() : null;
            }
            catch
            {
                return null;
            }
        }

        private void WireForceUpdateDownloadButton(GameObject forceUpdateRoot)
        {
            if (forceUpdateRoot == null) return;
            if (string.IsNullOrWhiteSpace(_downloadButtonPath)) return;

            var t = forceUpdateRoot.transform.Find(_downloadButtonPath);
            if (t == null) return;

            var button = t.GetComponent<UnityEngine.UI.Button>();
            if (button == null) return;

            if (_downloadButton != button)
            {
                _downloadButton = button;
                _downloadButton.onClick.RemoveAllListeners();
            }
            else
            {
                _downloadButton.onClick.RemoveAllListeners();
            }

            if (string.IsNullOrWhiteSpace(_storeUrl))
            {
                _downloadButton.interactable = false;
                return;
            }

            _downloadButton.interactable = true;
            _downloadButton.onClick.AddListener(() =>
            {
                var open = OpenUrlProvider ?? Application.OpenURL;
                open?.Invoke(_storeUrl);
            });
        }

        private static IEnumerator FadeCanvasGroup(CanvasGroup group, float from, float to, float durationSeconds)
        {
            if (group == null) yield break;
            if (durationSeconds <= 0f)
            {
                group.alpha = to;
                yield break;
            }

            group.alpha = from;

            var t = 0f;
            while (t < durationSeconds)
            {
                t += Time.unscaledDeltaTime;
                var a = Mathf.Clamp01(t / durationSeconds);
                group.alpha = Mathf.Lerp(from, to, a);
                yield return null;
            }

            group.alpha = to;
        }

        private bool WasContinuePressedThisFrame()
        {
            if (ContinuePressedThisFrameProvider != null)
            {
                return ContinuePressedThisFrameProvider();
            }

#if ENABLE_INPUT_SYSTEM
            // New Input System: support mouse click or any touch press.
            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) return true;
            if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame) return true;
            if (Pen.current != null && Pen.current.tip.wasPressedThisFrame) return true;
#endif

            // Fallback for cases where Legacy Input is enabled.
#if ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetMouseButtonDown(0)) return true;
            if (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began) return true;
#endif

            return false;
        }
    }
}
