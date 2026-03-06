using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AIWarsIdle.UI.Generators
{
    public sealed class UpgradeModeSelector : MonoBehaviour
    {
        private const string PlayerPrefsKey = "ui_upgrade_mode";

        [Header("UI")]
        [SerializeField] private Button _cycleButton;
        [SerializeField] private TMP_Text _label;

        [Header("Behavior")]
        [Tooltip("If enabled, persists selection to PlayerPrefs.")]
        [SerializeField] private bool _persistSelection = true;

        [Tooltip("Optional. If set, label will be formatted with selected mode text.")]
        [SerializeField] private string _labelFormat = "UPGRADE MODE\n{0}";

        public UpgradeMode CurrentMode { get; private set; } = UpgradeMode.X1;

        public event Action<UpgradeMode> ModeChanged;

        private void Awake()
        {
            if (_persistSelection && PlayerPrefs.HasKey(PlayerPrefsKey))
            {
                CurrentMode = IntToMode(PlayerPrefs.GetInt(PlayerPrefsKey, (int)UpgradeMode.X1));
            }

            ApplyLabel();

            if (_cycleButton != null)
            {
                _cycleButton.onClick.RemoveListener(OnCycleClicked);
                _cycleButton.onClick.AddListener(OnCycleClicked);
            }
        }

        private void OnDestroy()
        {
            if (_cycleButton != null)
            {
                _cycleButton.onClick.RemoveListener(OnCycleClicked);
            }
        }

        private void OnCycleClicked()
        {
            var next = CurrentMode switch
            {
                UpgradeMode.X1 => UpgradeMode.X10,
                UpgradeMode.X10 => UpgradeMode.Max,
                _ => UpgradeMode.X1,
            };

            SetMode(next);
        }

        public void SetMode(UpgradeMode mode)
        {
            if (CurrentMode == mode) return;
            CurrentMode = mode;

            if (_persistSelection)
            {
                PlayerPrefs.SetInt(PlayerPrefsKey, (int)CurrentMode);
                PlayerPrefs.Save();
            }

            ApplyLabel();
            ModeChanged?.Invoke(CurrentMode);
        }

        private void ApplyLabel()
        {
            if (_label == null) return;
            var modeText = CurrentMode switch
            {
                UpgradeMode.X1 => "x1",
                UpgradeMode.X10 => "x10",
                UpgradeMode.Max => "max",
                _ => "x1",
            };

            _label.text = string.IsNullOrWhiteSpace(_labelFormat) ? modeText : string.Format(_labelFormat, modeText);
        }

        private static UpgradeMode IntToMode(int value)
        {
            return value switch
            {
                (int)UpgradeMode.X1 => UpgradeMode.X1,
                (int)UpgradeMode.X10 => UpgradeMode.X10,
                (int)UpgradeMode.Max => UpgradeMode.Max,
                _ => UpgradeMode.X1,
            };
        }
    }
}

