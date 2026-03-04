using System;
using System.Collections;
using UnityEngine;

namespace AIWarsIdle.UI.Splash
{
    public sealed class ResumePolicyController : MonoBehaviour
    {
        [Header("Roots")]
        [SerializeField] private GameObject _splashRoot;
        [SerializeField] private GameObject _hubRoot;

        [Header("Splash FX")]
        [Tooltip("Optional. If null, a CanvasGroup will be searched on SplashRoot (or added at runtime).")]
        [SerializeField] private CanvasGroup _splashCanvasGroup;

        [Tooltip("Fade-out duration when leaving Splash (seconds). Set 0 for instant switch.")]
        [SerializeField] private float _splashFadeOutSeconds = 0.25f;

        [Header("Policy")]
        [Tooltip("If app resumes after >= this threshold, show Splash and route to Hub.")]
        [SerializeField] private float _resumeToSplashThresholdMinutes = 10f;

        [Tooltip("Optional: keep Splash visible for at least this many seconds (useful for layout / async checks).")]
        [SerializeField] private float _minimumSplashSeconds = 0f;

        [Tooltip("On cold start, show Splash and route to Hub.")]
        [SerializeField] private bool _showSplashOnColdStart = true;

        private long _lastPausedAtUnixSecondsUtc;
        private Coroutine _routingCoroutine;

        private void Start()
        {
            if (_showSplashOnColdStart)
            {
                RouteToHubThroughSplash();
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
                RouteToHubThroughSplash();
            }
        }

        public void RouteToHubThroughSplash()
        {
            if (_splashRoot == null || _hubRoot == null)
            {
                Debug.LogWarning($"{nameof(ResumePolicyController)}: Missing references. Assign SplashRoot and HubRoot.");
                return;
            }

            if (_routingCoroutine != null)
            {
                StopCoroutine(_routingCoroutine);
            }

            _routingCoroutine = StartCoroutine(RouteRoutine());
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

            if (_minimumSplashSeconds > 0f)
            {
                yield return new WaitForSeconds(_minimumSplashSeconds);
            }
            else
            {
                yield return null;
            }

            SetActiveSafe(_hubRoot, true);

            if (_splashFadeOutSeconds > 0f && splashCanvasGroup != null)
            {
                yield return FadeCanvasGroup(splashCanvasGroup, from: 1f, to: 0f, _splashFadeOutSeconds);
                splashCanvasGroup.interactable = false;
                splashCanvasGroup.blocksRaycasts = false;
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

        private static long NowUnixSecondsUtc()
        {
            return DateTimeOffset.UtcNow.ToUnixTimeSeconds();
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
    }
}
