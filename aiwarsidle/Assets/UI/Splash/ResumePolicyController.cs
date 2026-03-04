using System;
using System.Collections;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace AIWarsIdle.UI.Splash
{
    public sealed class ResumePolicyController : MonoBehaviour
    {
        public Func<long> NowUnixSecondsUtcProvider { get; set; }
        public Func<bool> ContinuePressedThisFrameProvider { get; set; }

        [Header("Roots")]
        [SerializeField] private GameObject _splashRoot;
        [SerializeField] private GameObject _hubRoot;

        [Header("Splash FX")]
        [Tooltip("Optional. If null, a CanvasGroup will be searched on SplashRoot (or added at runtime).")]
        [SerializeField] private CanvasGroup _splashCanvasGroup;

        [Tooltip("Fade-out duration when leaving Splash (seconds). Set 0 for instant switch.")]
        [SerializeField] private float _splashFadeOutSeconds = 0.25f;

        [Header("Splash UI")]
        [Tooltip("Optional. If null, will look for a child named 'touch_continue_dim' under SplashRoot.")]
        [SerializeField] private GameObject _touchContinueDim;

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

        public GameObject SplashRoot { get => _splashRoot; set => _splashRoot = value; }
        public GameObject HubRoot { get => _hubRoot; set => _hubRoot = value; }
        public GameObject TouchContinueDim { get => _touchContinueDim; set => _touchContinueDim = value; }
        public float ResumeToSplashThresholdMinutes { get => _resumeToSplashThresholdMinutes; set => _resumeToSplashThresholdMinutes = value; }
        public float MinimumSplashSeconds { get => _minimumSplashSeconds; set => _minimumSplashSeconds = value; }
        public float SplashFadeOutSeconds { get => _splashFadeOutSeconds; set => _splashFadeOutSeconds = value; }
        public bool ShowSplashOnColdStart { get => _showSplashOnColdStart; set => _showSplashOnColdStart = value; }

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

            if (_minimumSplashSeconds > 0f)
            {
                yield return new WaitForSeconds(_minimumSplashSeconds);
            }
            else
            {
                yield return null;
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
                SetActiveSafe(_hubRoot, true);
                yield return FadeCanvasGroup(splashCanvasGroup, from: 1f, to: 0f, _splashFadeOutSeconds);
                splashCanvasGroup.interactable = false;
                splashCanvasGroup.blocksRaycasts = false;
            }
            else
            {
                SetActiveSafe(_hubRoot, true);
            }

            if (touchContinue != null)
            {
                SetActiveSafe(touchContinue, false);
            }

            SetActiveSafe(_splashRoot, false);

            _routingCoroutine = null;
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
