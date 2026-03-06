using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AIWarsIdle.UI.Generators
{
    public sealed class OverclockButtonController : MonoBehaviour
    {
        [Header("Context")]
        [SerializeField] private GeneratorsScreenContext _context;

        [Header("UI")]
        [SerializeField] private Button _button;
        [SerializeField] private TMP_Text _chargesText;
        [SerializeField] private TMP_Text _timerText;
        [SerializeField] private Image _progressFill;

        [Header("Behavior")]
        [SerializeField] private float _clickDebounceSeconds = 0.12f;
        [SerializeField] private float _refreshIntervalSeconds = 0.25f;

        private float _nextClickAllowedAt;
        private float _carry;

        private void Awake()
        {
            if (_context == null) _context = GetComponentInParent<GeneratorsScreenContext>();
        }

        private void OnEnable()
        {
            _carry = 0;
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
        }

        private void Update()
        {
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
            if (loop == null) return;

            var state = loop.State;
            var now = loop.NowUnixSeconds;

            var maxCharges = loop.Overclock.MaxCharges;
            var charges = state.Overclock != null ? state.Overclock.Charges : 0;
            if (charges < 0) charges = 0;
            if (charges > maxCharges) charges = maxCharges;

            var isActive = loop.Overclock.IsActive(now);
            var canActivate = loop.Overclock.CanActivate(now);

            if (_chargesText != null) _chargesText.text = $"{charges}/{maxCharges}";

            if (_timerText != null)
            {
                if (isActive)
                {
                    var remaining = state.Overclock.ActiveUntilUnixSeconds - now;
                    if (remaining < 0) remaining = 0;
                    _timerText.text = UiFormat.Duration(remaining);
                }
                else if (charges < maxCharges)
                {
                    var nextAt = state.Overclock.NextChargeAtUnixSeconds;
                    var remaining = nextAt <= 0 ? 0 : nextAt - now;
                    if (remaining < 0) remaining = 0;
                    _timerText.text = UiFormat.Duration(remaining);
                }
                else
                {
                    _timerText.text = "READY";
                }
            }

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
