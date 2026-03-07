using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AIWarsIdle.UI.Generators
{
    public sealed class HudController : MonoBehaviour
    {
        [Header("Context")]
        [SerializeField] private GeneratorsScreenContext _context;

        [Header("Texts")]
        [SerializeField] private TMP_Text _softCurrencyText;
        [SerializeField] private TMP_Text _premiumCurrencyText;
        [SerializeField] private TMP_Text _ppsText;
        [SerializeField] private TMP_Text _lifetimeProducedText;
        [SerializeField] private TMP_Text _prestigeProgressText;
        [SerializeField] private TMP_Text _prestigeLevelText;

        [Header("PPS Pulse (Overclock)")]
        [SerializeField] private bool _pulsePpsWhenOverclockActive = true;
        [SerializeField] private RectTransform _ppsPulseTarget;
        [SerializeField] private float _ppsPulseScaleAmplitude = 0.12f;
        [SerializeField] private float _ppsPulseFrequencyHz = 6f;
        [SerializeField, Range(0f, 1f)] private float _ppsPulseToWhite = 0.35f;

        [Header("Prestige Progress (optional)")]
        [SerializeField] private Slider _prestigeSlider;
        [SerializeField] private Image _prestigeFillImage;

        [Header("Refresh")]
        [SerializeField] private float _refreshIntervalSeconds = 0.25f;

        private float _carry;
        private bool _pulseInitialized;
        private Vector3 _ppsBaseScale;
        private TMP_Text[] _ppsPulseTexts;
        private Color[] _ppsBaseColors;

        private void Awake()
        {
            if (_context == null) _context = GetComponentInParent<GeneratorsScreenContext>();
            if (_ppsPulseTarget == null && _ppsText != null) _ppsPulseTarget = _ppsText.transform.parent as RectTransform;
        }

        private void OnEnable()
        {
            _carry = 0;
            EnsurePulseInitialized();
            ResetPpsPulse();
            Refresh();
        }

        private void Update()
        {
            ApplyPpsPulse();
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

        private void OnDisable()
        {
            ResetPpsPulse();
        }

        private void EnsurePulseInitialized()
        {
            if (_pulseInitialized) return;
            _pulseInitialized = true;

            if (_ppsPulseTarget == null)
            {
                _ppsPulseTexts = null;
                _ppsBaseColors = null;
                _ppsBaseScale = Vector3.one;
                return;
            }

            _ppsBaseScale = _ppsPulseTarget.localScale;
            _ppsPulseTexts = _ppsPulseTarget.GetComponentsInChildren<TMP_Text>(includeInactive: true);
            _ppsBaseColors = new Color[_ppsPulseTexts.Length];
            for (var i = 0; i < _ppsPulseTexts.Length; i++)
            {
                _ppsBaseColors[i] = _ppsPulseTexts[i] != null ? _ppsPulseTexts[i].color : Color.white;
            }
        }

        private void ResetPpsPulse()
        {
            if (_ppsPulseTarget != null) _ppsPulseTarget.localScale = _ppsBaseScale;
            if (_ppsPulseTexts == null || _ppsBaseColors == null) return;
            for (var i = 0; i < _ppsPulseTexts.Length && i < _ppsBaseColors.Length; i++)
            {
                if (_ppsPulseTexts[i] == null) continue;
                _ppsPulseTexts[i].color = _ppsBaseColors[i];
            }
        }

        private void ApplyPpsPulse()
        {
            if (!_pulsePpsWhenOverclockActive || _ppsPulseTarget == null)
            {
                ResetPpsPulse();
                return;
            }

            var loop = _context != null ? _context.Loop : null;
            if (loop == null && _context != null && _context.Bootstrapper != null)
            {
                _context.Bootstrapper.Initialize();
                loop = _context.Loop;
            }
            if (loop == null)
            {
                ResetPpsPulse();
                return;
            }

            var now = loop.NowUnixSeconds;
            var isActive = loop.Overclock.IsActive(now);
            if (!isActive)
            {
                ResetPpsPulse();
                return;
            }

            var hz = Mathf.Max(0.1f, _ppsPulseFrequencyHz);
            var t01 = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * (Mathf.PI * 2f * hz));

            var amp = Mathf.Clamp(_ppsPulseScaleAmplitude, 0f, 0.5f);
            var scale = _ppsBaseScale * (1f + amp * (0.35f + 0.65f * t01));
            _ppsPulseTarget.localScale = scale;

            if (_ppsPulseTexts == null || _ppsBaseColors == null) return;
            var toWhite = Mathf.Clamp01(_ppsPulseToWhite) * (0.35f + 0.65f * t01);
            for (var i = 0; i < _ppsPulseTexts.Length && i < _ppsBaseColors.Length; i++)
            {
                var txt = _ppsPulseTexts[i];
                if (txt == null) continue;
                txt.color = Color.Lerp(_ppsBaseColors[i], Color.white, toWhite);
            }
        }

        public void Refresh()
        {
            var loop = _context != null ? _context.Loop : null;
            if (loop == null) return;

            var state = loop.State;
            var now = loop.NowUnixSeconds;

            if (_softCurrencyText != null) _softCurrencyText.text = UiFormat.Compact(state.SoftCurrency);
            if (_premiumCurrencyText != null) _premiumCurrencyText.text = state.PremiumCurrency.ToString();

            if (_ppsText != null)
            {
                var pps = loop.Production.CalculateProductionPerSecond(now);
                _ppsText.text = UiFormat.Compact(pps);
            }

            if (_lifetimeProducedText != null)
            {
                _lifetimeProducedText.text = UiFormat.Compact(state.LifetimeEarnedSoftCurrency);
            }

            if (_prestigeLevelText != null)
            {
                _prestigeLevelText.text = state.PrestigeCount.ToString();
            }

            var threshold = loop.Prestige.GetPrestigeThreshold();
            var earnedSinceLast = state.LifetimeEarnedSoftCurrency - state.LifetimeEarnedSoftCurrencyAtLastPrestige;
            if (earnedSinceLast < 0) earnedSinceLast = 0;
            var progress = threshold <= 0 ? 0f : Mathf.Clamp01((float)(earnedSinceLast / threshold));

            if (_prestigeProgressText != null)
            {
                var percent = Mathf.RoundToInt(progress * 100f);
                _prestigeProgressText.text = $"{percent}%";
            }

            if (_prestigeSlider != null) _prestigeSlider.value = progress;
            if (_prestigeFillImage != null) _prestigeFillImage.fillAmount = progress;
        }
    }
}
