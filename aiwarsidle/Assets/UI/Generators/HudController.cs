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

        [Header("Prestige Progress (optional)")]
        [SerializeField] private Slider _prestigeSlider;
        [SerializeField] private Image _prestigeFillImage;

        [Header("Refresh")]
        [SerializeField] private float _refreshIntervalSeconds = 0.25f;

        private float _carry;

        private void Awake()
        {
            if (_context == null) _context = GetComponentInParent<GeneratorsScreenContext>();
        }

        private void OnEnable()
        {
            _carry = 0;
            Refresh();
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

            var threshold = loop.Prestige.GetPrestigeThreshold();
            var progress = threshold <= 0 ? 0f : (float)Mathf.Clamp01((float)(state.LifetimeEarnedSoftCurrency / threshold));

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

