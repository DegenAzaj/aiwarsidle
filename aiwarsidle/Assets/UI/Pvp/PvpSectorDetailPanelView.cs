using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AIWarsIdle.UI.Pvp
{
    public sealed class PvpSectorDetailPanelView : MonoBehaviour
    {
        [SerializeField] private TMP_Text _title;
        [SerializeField] private TMP_Text _owner;
        [SerializeField] private TMP_Text _bonus;
        [SerializeField] private TMP_Text _stability;
        [SerializeField] private TMP_Text _preview;
        [SerializeField] private TMP_Text _position;
        [SerializeField] private TMP_Text _status;
        [SerializeField] private Button _strategyButton;
        [SerializeField] private TMP_Text _strategyButtonLabel;
        [SerializeField] private Button _actionButton;
        [SerializeField] private TMP_Text _actionButtonLabel;
        [SerializeField] private TMP_Text _hint;
        [SerializeField] private Color _statusReadyColor = new(0.45f, 0.88f, 0.60f, 1f);
        [SerializeField] private Color _statusBlockedColor = new(0.96f, 0.40f, 0.40f, 1f);

        private Color _statusDefaultColor = Color.white;

        private Action _onCycleStrategy;
        private Action _onAttack;

        public void Initialize(
            TMP_Text title,
            TMP_Text owner,
            TMP_Text bonus,
            TMP_Text stability,
            TMP_Text preview,
            TMP_Text position,
            TMP_Text status,
            Button strategyButton,
            Button actionButton,
            TMP_Text hint,
            Action onCycleStrategy,
            Action onAttack)
        {
            _title = title;
            _owner = owner;
            _bonus = bonus;
            _stability = stability;
            _preview = preview;
            _position = position;
            _status = status;
            _strategyButton = strategyButton;
            _actionButton = actionButton;
            _hint = hint;
            _strategyButtonLabel = ResolveButtonLabel(_strategyButton);
            _actionButtonLabel = ResolveButtonLabel(_actionButton);
            SetActions(onCycleStrategy, onAttack);
        }

        private void Awake()
        {
            ResolveOptionalReferences();
            ApplyStaticVisibility();
            CacheDefaultColors();
            WireButtons();
        }

        private void OnValidate()
        {
            ResolveOptionalReferences();
            ApplyStaticVisibility();
            CacheDefaultColors();
        }

        public void SetActions(Action onCycleStrategy, Action onAttack)
        {
            _onCycleStrategy = onCycleStrategy;
            _onAttack = onAttack;
            WireButtons();
        }

        public void ShowEmpty()
        {
            gameObject.SetActive(true);
            if (_title != null) _title.text = "Sector";
            if (_owner != null) _owner.text = "Owner: -";
            if (_bonus != null) _bonus.text = "Bonus: -";
            if (_stability != null) _stability.text = "Stability: -";
            if (_status != null)
            {
                _status.text = "Select a sector on the map.";
                _status.color = _statusDefaultColor;
            }
            if (_hint != null) _hint.text = "Hexes are generated from radius and runtime map data.";
            if (_strategyButton != null) _strategyButton.interactable = false;
            if (_strategyButtonLabel != null) _strategyButtonLabel.text = "Strategy: Stable";
            if (_actionButton != null) _actionButton.interactable = false;
            if (_actionButtonLabel != null) _actionButtonLabel.text = BuildActionButtonLabel("ATTACK", "BREACH CHANCE: -");
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }

        internal void ShowSector(HexGridRenderer.SectorDetailState detailState)
        {
            gameObject.SetActive(true);
            if (_title != null) _title.text = BuildNodeTitle(detailState.Sector.Coord.Q, detailState.Sector.Coord.R);
            if (_owner != null) _owner.text = detailState.OwnerText;
            if (_bonus != null) _bonus.text = detailState.BonusText;
            if (_stability != null) _stability.text = detailState.StabilityText;
            if (_status != null)
            {
                _status.text = detailState.StatusText;
                _status.color = detailState.CanAttack ? _statusReadyColor : _statusBlockedColor;
            }
            if (_hint != null) _hint.text = detailState.HintText;
            if (_strategyButton != null) _strategyButton.interactable = true;
            if (_strategyButtonLabel != null) _strategyButtonLabel.text = detailState.StrategyLabel;
            if (_actionButton != null) _actionButton.interactable = detailState.CanAttack;
            if (_actionButtonLabel != null) _actionButtonLabel.text = BuildActionButtonLabel(detailState.ActionLabel, detailState.PreviewText);
        }

        private void ResolveOptionalReferences()
        {
            _strategyButtonLabel ??= ResolveButtonLabel(_strategyButton);
            _actionButtonLabel ??= ResolveButtonLabel(_actionButton);
        }

        private void CacheDefaultColors()
        {
            if (_status != null)
            {
                _statusDefaultColor = _status.color;
            }
        }

        private void ApplyStaticVisibility()
        {
            if (_preview != null)
            {
                _preview.gameObject.SetActive(false);
            }

            if (_position != null)
            {
                _position.gameObject.SetActive(false);
            }
        }

        private void WireButtons()
        {
            WireButton(_strategyButton, () => _onCycleStrategy?.Invoke());
            WireButton(_actionButton, () => _onAttack?.Invoke());
        }

        private static TMP_Text ResolveButtonLabel(Button button)
        {
            return button != null
                ? button.GetComponentInChildren<TMP_Text>(includeInactive: true)
                : null;
        }

        private static void WireButton(Button button, Action callback)
        {
            if (button == null) return;
            button.onClick.RemoveAllListeners();
            if (callback != null)
            {
                button.onClick.AddListener(() => callback());
            }
        }

        private static string BuildNodeTitle(int q, int r)
        {
            return $"Node {FormatNodeAxis(q)} {FormatNodeAxis(r)}";
        }

        private static string BuildActionButtonLabel(string actionLabel, string previewText)
        {
            if (string.IsNullOrWhiteSpace(previewText))
            {
                return actionLabel ?? string.Empty;
            }

            return $"{actionLabel}\n{previewText}";
        }

        private static string FormatNodeAxis(int value)
        {
            if (value >= 0)
            {
                return value.ToString();
            }

            return ConvertNegativeIndexToLetters(-value);
        }

        private static string ConvertNegativeIndexToLetters(int index)
        {
            if (index <= 0)
            {
                return "0";
            }

            var result = string.Empty;
            while (index > 0)
            {
                index--;
                result = (char)('A' + (index % 26)) + result;
                index /= 26;
            }

            return result;
        }
    }
}
