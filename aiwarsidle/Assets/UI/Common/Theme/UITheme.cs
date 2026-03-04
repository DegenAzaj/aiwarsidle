using UnityEngine;

namespace AIWarsIdle.UI.Theme
{
    public enum UIThemeColor
    {
        Background = 0,
        Surface = 1,
        Border = 2,
        Light = 3,
        InnerShadow = 4,

        TextPrimary = 10,
        TextSecondary = 11,

        Accent = 20,
        AccentHover = 21,
        AccentPressed = 22,

        Success = 30,
        Warning = 31,
        Danger = 32,

        Disabled = 40
    }

    [CreateAssetMenu(menuName = "AI Wars Idle/UI/Theme", fileName = "UITheme")]
    public sealed class UITheme : ScriptableObject
    {
        [Header("Surfaces")]
        [SerializeField] private Color _background = new(0.06f, 0.08f, 0.12f, 1f);
        [SerializeField] private Color _surface = new(0.10f, 0.12f, 0.18f, 1f);
        [SerializeField] private Color _light = new(0.14f, 0.16f, 0.24f, 1f);
        [SerializeField] private Color _innerShadow = new(0.00f, 0.00f, 0.00f, 0.35f);
        [SerializeField] private Color _border = new(0.20f, 0.22f, 0.30f, 1f);

        [Header("Text")]
        [SerializeField] private Color _textPrimary = new(0.95f, 0.96f, 0.98f, 1f);
        [SerializeField] private Color _textSecondary = new(0.70f, 0.73f, 0.80f, 1f);

        [Header("Accent")]
        [SerializeField] private Color _accent = new(0.22f, 0.60f, 1.00f, 1f);
        [SerializeField] private Color _accentHover = new(0.30f, 0.70f, 1.00f, 1f);
        [SerializeField] private Color _accentPressed = new(0.15f, 0.50f, 0.90f, 1f);

        [Header("Status")]
        [SerializeField] private Color _success = new(0.30f, 0.85f, 0.55f, 1f);
        [SerializeField] private Color _warning = new(1.00f, 0.80f, 0.25f, 1f);
        [SerializeField] private Color _danger = new(1.00f, 0.35f, 0.35f, 1f);

        [Header("State")]
        [SerializeField] private Color _disabled = new(0.35f, 0.37f, 0.42f, 1f);

        public Color Get(UIThemeColor color)
        {
            return color switch
            {
                UIThemeColor.Background => _background,
                UIThemeColor.Surface => _surface,
                UIThemeColor.Light => _light,
                UIThemeColor.InnerShadow => _innerShadow,
                UIThemeColor.Border => _border,
                UIThemeColor.TextPrimary => _textPrimary,
                UIThemeColor.TextSecondary => _textSecondary,
                UIThemeColor.Accent => _accent,
                UIThemeColor.AccentHover => _accentHover,
                UIThemeColor.AccentPressed => _accentPressed,
                UIThemeColor.Success => _success,
                UIThemeColor.Warning => _warning,
                UIThemeColor.Danger => _danger,
                UIThemeColor.Disabled => _disabled,
                _ => _surface
            };
        }
    }
}
