using System;
using TMPro;
using AIWarsIdle.UI.Common;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AIWarsIdle.UI.Pvp
{
    public sealed class PvpHudTemplateView : MonoBehaviour
    {
        [SerializeField] private TMP_Text _attacks;
        [SerializeField] private TMP_Text _matchEnds;
        [SerializeField] private TMP_Text _power;
        [SerializeField] private TMP_Text _scenePowerText;
        [SerializeField] private TMP_Text _tableTitle;
        [SerializeField] private TMP_Text _liveScoreTable;
        [SerializeField] private Button _powerInfoButton;
        [SerializeField] private Button _scenePowerInfoButton;
        [SerializeField] private GenericTooltipView _powerTooltipPrefab;
        [SerializeField] private TMP_Text _powerTooltip;
        [SerializeField] private string _powerTooltipTitle = "PvP Power";
        [TextArea(2, 4)]
        [SerializeField] private string _powerTooltipBody = "PvpPower = prestige + permanent upgrades + sectors + small production bonus.\nSubscription boosts production, but does not directly raise PvpPower.";

        private Action _showPowerInfo;
        private Action _hidePowerInfo;
        private GenericTooltipView _powerTooltipInstance;

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
            _showPowerInfo = togglePowerInfo;
            _hidePowerInfo = () => SetPowerInfoVisible(false);
            WireButton();
        }

        private void Awake()
        {
            ResolveSceneOverrides();
            WireButton();
        }

        private void OnValidate()
        {
            ResolveSceneOverrides();

            if (_powerTooltip != null)
            {
                _powerTooltip.gameObject.SetActive(false);
            }
        }

        private void OnDisable()
        {
            if (_powerTooltipInstance != null)
            {
                _powerTooltipInstance.Hide();
            }
        }

        public void SetTogglePowerInfo(Action togglePowerInfo)
        {
            SetPowerInfoActions(togglePowerInfo, () => SetPowerInfoVisible(false));
        }

        public void SetPowerInfoActions(Action showPowerInfo, Action hidePowerInfo)
        {
            _showPowerInfo = showPowerInfo;
            _hidePowerInfo = hidePowerInfo;
            ResolveSceneOverrides();
            WireButton();
        }

        public void SetPowerTooltipContent(string title, string body)
        {
            _powerTooltipTitle = title ?? string.Empty;
            _powerTooltipBody = body ?? string.Empty;
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
            ResolveSceneOverrides();
            if (_attacks != null) _attacks.text = attacks;
            if (_matchEnds != null) _matchEnds.text = matchEnds;
            if (_power != null) _power.text = power;
            if (_scenePowerText != null) _scenePowerText.text = power;
            if (_tableTitle != null) _tableTitle.text = tableTitle;
            if (_liveScoreTable != null) _liveScoreTable.text = ApplyTableHeaderColor(liveScoreTable);
            SetPowerInfoVisible(infoVisible);
        }

        public void SetPowerInfoVisible(bool visible)
        {
            if (visible)
            {
                if (EnsurePowerTooltipInstance())
                {
                    _powerTooltipInstance.Show(_powerTooltipTitle, _powerTooltipBody);
                }
                else if (_powerTooltip != null)
                {
                    _powerTooltip.gameObject.SetActive(true);
                }

                return;
            }

            if (_powerTooltipInstance != null)
            {
                _powerTooltipInstance.Hide();
            }

            if (_powerTooltip != null)
            {
                _powerTooltip.gameObject.SetActive(false);
            }
        }

        private void WireButton()
        {
            WireButton(_powerInfoButton);
            WireButton(_scenePowerInfoButton);
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

        private void ResolveSceneOverrides()
        {
            _scenePowerText ??= FindTextByName("pvp_power_text");

            if (_scenePowerInfoButton == null)
            {
                var askIcon = FindChildByName("ask_ico");
                if (askIcon != null)
                {
                    _scenePowerInfoButton = askIcon.GetComponent<Button>();
                    _scenePowerInfoButton ??= askIcon.GetComponentInChildren<Button>(true);
                }
            }
        }

        private bool EnsurePowerTooltipInstance()
        {
            if (_powerTooltipInstance != null) return true;

            var prefab = _powerTooltipPrefab;
            if (prefab == null)
            {
                prefab = Resources.Load<GenericTooltipView>("UI/GenericTooltip");
            }

            if (prefab == null) return false;

            var canvas = ResolveTooltipCanvas();
            if (canvas == null) return false;

            _powerTooltipInstance = Instantiate(prefab, canvas.transform, worldPositionStays: false);
            _powerTooltipInstance.name = prefab.name;
            _powerTooltipInstance.Hide();
            return true;
        }

        private Canvas ResolveTooltipCanvas()
        {
            var source = _scenePowerInfoButton != null
                ? _scenePowerInfoButton.transform
                : _powerInfoButton != null
                    ? _powerInfoButton.transform
                    : transform;

            return source.GetComponentInParent<Canvas>();
        }

        private void WireButton(Button button)
        {
            if (button == null) return;

            button.onClick.RemoveAllListeners();
            var holdTrigger = button.GetComponent<HoldTooltipTrigger>();
            if (holdTrigger == null)
            {
                holdTrigger = button.gameObject.AddComponent<HoldTooltipTrigger>();
            }

            holdTrigger.Bind(
                () => _showPowerInfo?.Invoke(),
                () => _hidePowerInfo?.Invoke());
        }

        private TMP_Text FindTextByName(string targetName)
        {
            if (string.IsNullOrEmpty(targetName)) return null;

            var texts = GetComponentsInChildren<TMP_Text>(true);
            for (var i = 0; i < texts.Length; i++)
            {
                var text = texts[i];
                if (text != null && string.Equals(text.name, targetName, StringComparison.Ordinal))
                {
                    return text;
                }
            }

            if (!TryGetSearchScene(out var scene)) return null;

            var roots = scene.GetRootGameObjects();
            for (var i = 0; i < roots.Length; i++)
            {
                var sceneTexts = roots[i].GetComponentsInChildren<TMP_Text>(true);
                for (var j = 0; j < sceneTexts.Length; j++)
                {
                    var text = sceneTexts[j];
                    if (text != null && string.Equals(text.name, targetName, StringComparison.Ordinal))
                    {
                        return text;
                    }
                }
            }

            return null;
        }

        private Transform FindChildByName(string targetName)
        {
            if (string.IsNullOrEmpty(targetName)) return null;

            var transforms = GetComponentsInChildren<Transform>(true);
            for (var i = 0; i < transforms.Length; i++)
            {
                var child = transforms[i];
                if (child != null && string.Equals(child.name, targetName, StringComparison.Ordinal))
                {
                    return child;
                }
            }

            if (!TryGetSearchScene(out var scene)) return null;

            var roots = scene.GetRootGameObjects();
            for (var i = 0; i < roots.Length; i++)
            {
                var sceneTransforms = roots[i].GetComponentsInChildren<Transform>(true);
                for (var j = 0; j < sceneTransforms.Length; j++)
                {
                    var child = sceneTransforms[j];
                    if (child != null && string.Equals(child.name, targetName, StringComparison.Ordinal))
                    {
                        return child;
                    }
                }
            }

            return null;
        }

        private bool TryGetSearchScene(out Scene scene)
        {
            scene = gameObject.scene;
            if (scene.IsValid() && scene.isLoaded)
            {
                return true;
            }

            scene = SceneManager.GetActiveScene();
            if (scene.IsValid() && scene.isLoaded)
            {
                return true;
            }

            return false;
        }
    }
}
