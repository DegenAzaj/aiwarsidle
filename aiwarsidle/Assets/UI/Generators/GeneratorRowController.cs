using System;
using AIWarsIdle.GameCore.Domain;
using AIWarsIdle.GameCore.Services;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AIWarsIdle.UI.Generators
{
    public sealed class GeneratorRowController : MonoBehaviour
    {
        [Header("Config")]
        [SerializeField] private int _generatorId;

        [Header("Context")]
        [SerializeField] private GeneratorsScreenContext _context;
        [SerializeField] private UpgradeModeSelector _mode;

        [Header("UI")]
        [SerializeField] private TMP_Text _nameText;
        [SerializeField] private TMP_Text _levelText;
        [SerializeField] private TMP_Text _ppsText;
        [SerializeField] private TMP_Text _costText;
        [SerializeField] private Button _upgradeButton;
        [SerializeField] private TMP_Text _upgradeButtonLabel;

        [Header("Behavior")]
        [SerializeField] private float _clickDebounceSeconds = 0.08f;

        [Header("Refresh")]
        [SerializeField] private float _refreshIntervalSeconds = 0.25f;

        private float _nextClickAllowedAt;
        private float _carry;
        private IDisposable _currencySub;
        private IDisposable _upgradeSub;

        public int GeneratorId => _generatorId;

        private void Awake()
        {
            if (_context == null) _context = GetComponentInParent<GeneratorsScreenContext>();
        }

        private void OnEnable()
        {
            _carry = 0;

            if (_upgradeButton != null)
            {
                _upgradeButton.onClick.RemoveListener(OnUpgradeClicked);
                _upgradeButton.onClick.AddListener(OnUpgradeClicked);
            }

            if (_mode != null)
            {
                _mode.ModeChanged -= OnModeChanged;
                _mode.ModeChanged += OnModeChanged;
            }

            SubscribeToEvents();
            Refresh();
        }

        private void OnDisable()
        {
            if (_upgradeButton != null) _upgradeButton.onClick.RemoveListener(OnUpgradeClicked);
            if (_mode != null) _mode.ModeChanged -= OnModeChanged;
            UnsubscribeFromEvents();
        }

        private void Update()
        {
            if (_refreshIntervalSeconds <= 0f) return;

            _carry += Time.unscaledDeltaTime;
            if (_carry < _refreshIntervalSeconds) return;
            _carry = 0;
            Refresh();
        }

        private void OnModeChanged(UpgradeMode obj)
        {
            Refresh();
        }

        private void SubscribeToEvents()
        {
            UnsubscribeFromEvents();

            var loop = _context != null ? _context.Loop : null;
            if (loop == null) return;
            if (loop.EventBus == null) return;

            _currencySub = loop.EventBus.Subscribe<CurrencyChangedEvent>(_ => Refresh());
            _upgradeSub = loop.EventBus.Subscribe<GeneratorUpgradedEvent>(evt =>
            {
                if (evt.GeneratorId == _generatorId) Refresh();
            });
        }

        private void UnsubscribeFromEvents()
        {
            _currencySub?.Dispose();
            _currencySub = null;
            _upgradeSub?.Dispose();
            _upgradeSub = null;
        }

        public void Refresh()
        {
            var loop = _context != null ? _context.Loop : null;
            if (loop == null) return;

            if (_generatorId < 0 || _generatorId >= GameState.GeneratorCount) return;

            var state = loop.State;
            var now = loop.NowUnixSeconds;

            if (_nameText != null)
            {
                _nameText.text = _generatorId switch
                {
                    0 => "Generator 1",
                    1 => "Generator 2",
                    2 => "Generator 3",
                    3 => "Generator 4",
                    4 => "Generator 5",
                    _ => $"Generator {_generatorId + 1}",
                };
            }

            if (_levelText != null) _levelText.text = $"Level: {state.GeneratorLevels[_generatorId]}";

            if (_ppsText != null)
            {
                var pps = loop.Production.CalculateGeneratorProductionPerSecond(_generatorId, now);
                _ppsText.text = UiFormat.Compact(pps);
            }

            var cost = loop.Upgrades.GetUpgradeCost(_generatorId);
            if (_costText != null) _costText.text = UiFormat.Compact(cost);

            var canAffordAtLeastOne = loop.Economy.Balance >= cost;
            if (_upgradeButton != null) _upgradeButton.interactable = canAffordAtLeastOne;

            if (_upgradeButtonLabel != null && _mode != null)
            {
                _upgradeButtonLabel.text = _mode.CurrentMode switch
                {
                    UpgradeMode.X1 => "x1 Upgrade",
                    UpgradeMode.X10 => "x10 Upgrade",
                    _ => "Max Upgrade",
                };
            }
        }

        private void OnUpgradeClicked()
        {
            if (_clickDebounceSeconds > 0f && Time.unscaledTime < _nextClickAllowedAt) return;
            _nextClickAllowedAt = Time.unscaledTime + Mathf.Max(0f, _clickDebounceSeconds);

            var loop = _context != null ? _context.Loop : null;
            if (loop == null) return;
            if (_generatorId < 0 || _generatorId >= GameState.GeneratorCount) return;

            var mode = _mode != null ? _mode.CurrentMode : UpgradeMode.X1;
            _ = mode switch
            {
                UpgradeMode.X1 => loop.Upgrades.UpgradeGeneratorX1(_generatorId),
                UpgradeMode.X10 => loop.Upgrades.UpgradeGeneratorX10(_generatorId),
                _ => loop.Upgrades.UpgradeGeneratorMax(_generatorId),
            };

            Refresh();
        }
    }
}
