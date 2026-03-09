using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AIWarsIdle.UI.Pvp
{
    public sealed class PvpHudTemplateView : MonoBehaviour
    {
        [SerializeField] private TMP_Text _attacks;
        [SerializeField] private TMP_Text _matchEnds;
        [SerializeField] private TMP_Text _power;
        [SerializeField] private TMP_Text _tableTitle;
        [SerializeField] private TMP_Text _liveScoreTable;
        [SerializeField] private Button _powerInfoButton;
        [SerializeField] private TMP_Text _powerTooltip;

        private Action _togglePowerInfo;

        public void Initialize(
            TMP_Text attacks,
            TMP_Text matchEnds,
            TMP_Text power,
            TMP_Text tableTitle,
            TMP_Text liveScoreTable,
            Button powerInfoButton,
            TMP_Text powerTooltip,
            Action togglePowerInfo)
        {
            _attacks = attacks;
            _matchEnds = matchEnds;
            _power = power;
            _tableTitle = tableTitle;
            _liveScoreTable = liveScoreTable;
            _powerInfoButton = powerInfoButton;
            _powerTooltip = powerTooltip;
            _togglePowerInfo = togglePowerInfo;
            WireButton();
        }

        private void Awake()
        {
            WireButton();
        }

        private void OnValidate()
        {
            if (_powerTooltip != null)
            {
                _powerTooltip.gameObject.SetActive(false);
            }
        }

        public void SetTogglePowerInfo(Action togglePowerInfo)
        {
            _togglePowerInfo = togglePowerInfo;
            WireButton();
        }

        public void ShowPreview(int count, int radius, bool infoVisible)
        {
            ShowState(
                $"Preview map: {count} hexes",
                "03:00:00",
                "PvpPower preview unavailable.",
                "Live score",
                $"<mspace=0.58em>PLAYER       POWER SCORE\nYou            42.0     7\nPlayer 2       38.0     5\nPlayer 3       31.5     4\nRadius {radius}        --    --</mspace>",
                infoVisible);
        }

        public void ShowState(string attacks, string matchEnds, string power, string tableTitle, string liveScoreTable, bool infoVisible)
        {
            if (_attacks != null) _attacks.text = attacks;
            if (_matchEnds != null) _matchEnds.text = matchEnds;
            if (_power != null) _power.text = power;
            if (_tableTitle != null) _tableTitle.text = tableTitle;
            if (_liveScoreTable != null) _liveScoreTable.text = ApplyTableHeaderColor(liveScoreTable);
            SetPowerInfoVisible(infoVisible);
        }

        public void SetPowerInfoVisible(bool visible)
        {
            if (_powerTooltip != null) _powerTooltip.gameObject.SetActive(visible);
        }

        private void WireButton()
        {
            if (_powerInfoButton == null) return;

            _powerInfoButton.onClick.RemoveAllListeners();
            _powerInfoButton.onClick.AddListener(() => _togglePowerInfo?.Invoke());
        }

        private string ApplyTableHeaderColor(string liveScoreTable)
        {
            if (string.IsNullOrEmpty(liveScoreTable)) return liveScoreTable;
            if (_tableTitle == null) return liveScoreTable;

            var colorTag = ColorUtility.ToHtmlStringRGB(_tableTitle.color);
            const string mspaceOpen = "<mspace=0.58em>";
            const string mspaceClose = "</mspace>";

            if (liveScoreTable.StartsWith(mspaceOpen, StringComparison.Ordinal) &&
                liveScoreTable.EndsWith(mspaceClose, StringComparison.Ordinal))
            {
                var inner = liveScoreTable.Substring(mspaceOpen.Length, liveScoreTable.Length - mspaceOpen.Length - mspaceClose.Length);
                var newlineIndex = inner.IndexOf('\n');
                if (newlineIndex < 0)
                {
                    return $"{mspaceOpen}<color=#{colorTag}>{inner}</color>{mspaceClose}";
                }

                var header = inner.Substring(0, newlineIndex);
                var rest = inner.Substring(newlineIndex + 1);
                return $"{mspaceOpen}<color=#{colorTag}>{header}</color>\n{rest}{mspaceClose}";
            }

            var plainNewlineIndex = liveScoreTable.IndexOf('\n');
            if (plainNewlineIndex < 0)
            {
                return $"<color=#{colorTag}>{liveScoreTable}</color>";
            }

            var plainHeader = liveScoreTable.Substring(0, plainNewlineIndex);
            var plainRest = liveScoreTable.Substring(plainNewlineIndex + 1);
            return $"<color=#{colorTag}>{plainHeader}</color>\n{plainRest}";
        }
    }
}
