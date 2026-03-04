using UnityEngine;

namespace AIWarsIdle.UI.Theme
{
    [ExecuteAlways]
    public sealed class UIThemeProvider : MonoBehaviour
    {
        [SerializeField] private UITheme _theme;

        public UITheme Theme => _theme;

        private static UIThemeProvider _instance;
        public static UIThemeProvider Instance => _instance;

        private void Awake()
        {
            _instance = this;
        }

        private void OnEnable()
        {
            _instance = this;
        }

        private void OnDisable()
        {
            if (_instance == this) _instance = null;
        }
    }
}
