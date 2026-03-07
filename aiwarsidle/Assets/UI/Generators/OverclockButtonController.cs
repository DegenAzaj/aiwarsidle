using System;
using System.Collections.Generic;
using AIWarsIdle.Bootstrap;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityObject = UnityEngine.Object;

namespace AIWarsIdle.UI.Generators
{
    public sealed class OverclockButtonController : MonoBehaviour
    {
        [Header("Context")]
        [SerializeField] private GeneratorsScreenContext _context;

        [Header("UI")]
        [SerializeField] private Button _button;
        [SerializeField] private TMP_Text _labelText;
        [SerializeField] private TMP_Text _chargesText;
        [SerializeField] private TMP_Text _timerText;
        [SerializeField] private Image _progressFill;

        [Header("Glow (optional)")]
        [SerializeField] private GameObject _glowObject;
        [SerializeField] private bool _pulseGlowWhenActive = true;
        [SerializeField] private float _glowPulseScaleAmplitude = 0.10f;
        [SerializeField] private float _glowPulseFrequencyHz = 8f;
        [SerializeField, Range(0f, 1f)] private float _glowPulseAlphaAmplitude = 0.45f;

        [Header("Behavior")]
        [SerializeField] private float _clickDebounceSeconds = 0.12f;
        [SerializeField] private float _refreshIntervalSeconds = 0.25f;

        private float _nextClickAllowedAt;
        private float _carry;

        private bool _glowInitialized;
        private Transform _glowTransform;
        private Graphic _glowGraphic;
        private Vector3 _glowBaseScale;
        private Color _glowBaseColor;

        private void Awake()
        {
            if (_context == null) _context = GetComponentInParent<GeneratorsScreenContext>();
            if (_context != null) return;

#if UNITY_2023_1_OR_NEWER
            _context = UnityObject.FindFirstObjectByType<GeneratorsScreenContext>(FindObjectsInactive.Exclude);
            if (_context != null) return;
            _context = UnityObject.FindAnyObjectByType<GeneratorsScreenContext>(FindObjectsInactive.Exclude);
#else
            _context = UnityObject.FindObjectOfType<GeneratorsScreenContext>();
#endif
        }

        private void OnEnable()
        {
            _carry = 0;
            if (_labelText == null)
            {
                _labelText = _chargesText != null ? _chargesText : _timerText;
                if (_labelText == null) _labelText = GetComponentInChildren<TMP_Text>(includeInactive: true);
            }

            EnsureGlowInitialized();
            ResetGlow();

            if (_button != null)
            {
                _button.onClick.RemoveListener(OnClicked);
                _button.onClick.AddListener(OnClicked);
            }
            Refresh();
        }

        private void OnDisable()
        {
            if (_button != null) _button.onClick.RemoveListener(OnClicked);
            ResetGlow();
        }

        private void Update()
        {
            ApplyGlowPulse();

            if (_refreshIntervalSeconds <= 0f)
            {
                Refresh();
                return;
            }

            _carry += Time.unscaledDeltaTime;
            if (_carry < _refreshIntervalSeconds) return;
            _carry = 0;
            Refresh();
        }

        public void Refresh()
        {
            var loop = _context != null ? _context.Loop : null;
            if (loop == null && _context != null && _context.Bootstrapper != null)
            {
                _context.Bootstrapper.Initialize();
                loop = _context.Loop;
            }
            if (loop == null) return;

            var state = loop.State;
            var now = loop.NowUnixSeconds;

            var maxCharges = loop.Overclock.MaxCharges;
            var charges = state.Overclock != null ? state.Overclock.Charges : 0;
            if (charges < 0) charges = 0;
            if (charges > maxCharges) charges = maxCharges;

            var isActive = loop.Overclock.IsActive(now);
            var canActivate = loop.Overclock.CanActivate(now);

            var label = BuildLabel(loop, charges, maxCharges, now, isActive);
            if (_labelText != null) _labelText.text = label;
            if (_chargesText != null && _chargesText != _labelText) _chargesText.text = label;
            if (_timerText != null && _timerText != _labelText) _timerText.text = label;

            if (_button != null) _button.interactable = canActivate;

            if (_progressFill != null)
            {
                if (isActive)
                {
                    var remaining = state.Overclock.ActiveUntilUnixSeconds - now;
                    if (remaining < 0) remaining = 0;
                    var duration = Mathf.Max(1, loop.Overclock.DurationSeconds);
                    _progressFill.fillAmount = Mathf.Clamp01(remaining / (float)duration);
                }
                else if (charges < maxCharges && state.Overclock.NextChargeAtUnixSeconds > 0)
                {
                    var remaining = state.Overclock.NextChargeAtUnixSeconds - now;
                    if (remaining < 0) remaining = 0;
                    var regen = Mathf.Max(1, loop.Overclock.RegenSeconds);
                    _progressFill.fillAmount = 1f - Mathf.Clamp01(remaining / (float)regen);
                }
                else
                {
                    _progressFill.fillAmount = 1f;
                }
            }
        }

        private void EnsureGlowInitialized()
        {
            if (_glowInitialized) return;
            _glowInitialized = true;

            if (_glowObject == null)
            {
                _glowObject = FindFirstChildByName(gameObject, "Glow");
            }

            if (_glowObject == null)
            {
                _glowTransform = null;
                _glowGraphic = null;
                _glowBaseScale = Vector3.one;
                _glowBaseColor = Color.white;
                return;
            }

            _glowTransform = _glowObject.transform;
            _glowGraphic = _glowObject.GetComponent<Graphic>();
            _glowBaseScale = _glowTransform.localScale;
            _glowBaseColor = _glowGraphic != null ? _glowGraphic.color : Color.white;
        }

        private void ResetGlow()
        {
            if (_glowObject == null) return;
            _glowObject.SetActive(false);
            if (_glowTransform != null) _glowTransform.localScale = _glowBaseScale;
            if (_glowGraphic != null) _glowGraphic.color = _glowBaseColor;
        }

        private void ApplyGlowPulse()
        {
            if (_glowObject == null || !_pulseGlowWhenActive) return;

            var loop = _context != null ? _context.Loop : null;
            if (loop == null && _context != null && _context.Bootstrapper != null)
            {
                _context.Bootstrapper.Initialize();
                loop = _context.Loop;
            }
            if (loop == null)
            {
                if (_glowObject.activeSelf) ResetGlow();
                return;
            }

            var isActive = loop.Overclock.IsActive(loop.NowUnixSeconds);
            if (!isActive)
            {
                if (_glowObject.activeSelf) ResetGlow();
                return;
            }

            if (!_glowObject.activeSelf) _glowObject.SetActive(true);

            var hz = Mathf.Max(0.1f, _glowPulseFrequencyHz);
            var t01 = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * (Mathf.PI * 2f * hz));
            var shaped = 0.35f + 0.65f * t01;

            if (_glowTransform != null)
            {
                var amp = Mathf.Clamp(_glowPulseScaleAmplitude, 0f, 0.6f);
                _glowTransform.localScale = _glowBaseScale * (1f + amp * shaped);
            }

            if (_glowGraphic != null)
            {
                var aAmp = Mathf.Clamp01(_glowPulseAlphaAmplitude);
                var baseA = _glowBaseColor.a;
                var alphaMultiplier = Mathf.Lerp(1f - aAmp, 1f, shaped);
                var c = _glowBaseColor;
                c.a = Mathf.Clamp01(baseA * alphaMultiplier);
                _glowGraphic.color = c;
            }
        }

        private static GameObject FindFirstChildByName(GameObject root, string childName)
        {
            if (root == null) return null;
            if (string.IsNullOrWhiteSpace(childName)) return null;

            var q = new Queue<Transform>();
            q.Enqueue(root.transform);
            while (q.Count > 0)
            {
                var t = q.Dequeue();
                for (var i = 0; i < t.childCount; i++)
                {
                    var ch = t.GetChild(i);
                    if (ch == null) continue;
                    if (string.Equals(ch.name, childName, StringComparison.Ordinal))
                    {
                        return ch.gameObject;
                    }
                    q.Enqueue(ch);
                }
            }
            return null;
        }

        private static string BuildLabel(
            GameLoop loop,
            int charges,
            int maxCharges,
            long now,
            bool isActive)
        {
            if (isActive)
            {
                var state = loop.State;
                var remaining = state.Overclock.ActiveUntilUnixSeconds - now;
                if (remaining < 0) remaining = 0;
                var seconds = Mathf.Clamp(Mathf.CeilToInt(remaining), 1, Mathf.Max(1, loop.Overclock.DurationSeconds));
                return $"ACTIVATED\n{seconds}";
            }

            if (charges >= maxCharges || loop.State.Overclock == null)
            {
                return $"OVERCLOCK\n{charges}/{maxCharges}";
            }

            var nextAt = loop.State.Overclock.NextChargeAtUnixSeconds;
            var remainingToCharge = nextAt <= 0 ? 0 : nextAt - now;
            if (remainingToCharge < 0) remainingToCharge = 0;
            var timer = UiFormat.DurationMmSs(remainingToCharge);
            return $"OVERCLOCK\n{charges}/{maxCharges} {timer}";
        }

        private void OnClicked()
        {
            if (_clickDebounceSeconds > 0f && Time.unscaledTime < _nextClickAllowedAt) return;
            _nextClickAllowedAt = Time.unscaledTime + Mathf.Max(0f, _clickDebounceSeconds);

            var loop = _context != null ? _context.Loop : null;
            if (loop == null) return;

            var now = loop.NowUnixSeconds;
            loop.Overclock.Activate(now);
            Refresh();
        }
    }
}
