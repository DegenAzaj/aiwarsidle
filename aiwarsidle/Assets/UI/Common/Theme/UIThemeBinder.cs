using System;
using UnityEngine;
using UnityEngine.UI;

namespace AIWarsIdle.UI.Theme
{
    [ExecuteAlways]
    public sealed class UIThemeBinder : MonoBehaviour
    {
        [Serializable]
        public sealed class GraphicBinding
        {
            public Graphic Target;
            public UIThemeColor Color;
            public bool PreserveAlpha = true;
        }

        [Serializable]
        public sealed class OutlineBinding
        {
            public Outline Target;
            public UIThemeColor Color = UIThemeColor.Border;
            public bool PreserveAlpha = true;
        }

        [Serializable]
        public sealed class SelectableBinding
        {
            public Selectable Target;

            [Header("ColorBlock")]
            public UIThemeColor Normal = UIThemeColor.Accent;
            public UIThemeColor Highlighted = UIThemeColor.AccentHover;
            public UIThemeColor Pressed = UIThemeColor.AccentPressed;
            public UIThemeColor Selected = UIThemeColor.AccentHover;
            public UIThemeColor Disabled = UIThemeColor.Disabled;

            [Header("Multipliers")]
            [Range(0f, 1f)] public float DisabledAlpha = 0.5f;
            public bool PreserveAlpha = false;
        }

        [SerializeField] private UITheme _theme;
        [SerializeField] private bool _applyOnEnable = true;

        [Header("Bindings")]
        [SerializeField] private GraphicBinding[] _graphics = Array.Empty<GraphicBinding>();
        [SerializeField] private OutlineBinding[] _outlines = Array.Empty<OutlineBinding>();
        [SerializeField] private SelectableBinding[] _selectables = Array.Empty<SelectableBinding>();

        public void Apply()
        {
            var theme = ResolveTheme();
            if (theme == null) return;

            ApplyGraphics(theme);
            ApplyOutlines(theme);
            ApplySelectables(theme);
        }

        private UITheme ResolveTheme()
        {
            if (_theme != null) return _theme;

            var provider = UIThemeProvider.Instance;
            if (provider == null)
            {
                // ExecuteAlways order can make Instance unset during edit-time validation.
                // Fallback to a scene lookup.
                provider = FindObjectOfType<UIThemeProvider>(includeInactive: true);
            }

            return provider != null ? provider.Theme : null;
        }

        private void OnEnable()
        {
            if (!_applyOnEnable) return;
            Apply();
        }

        private void OnValidate()
        {
            Apply();
        }

        private void ApplyGraphics(UITheme theme)
        {
            if (_graphics == null) return;

            for (var i = 0; i < _graphics.Length; i++)
            {
                var binding = _graphics[i];
                if (binding == null || binding.Target == null) continue;

                var c = theme.Get(binding.Color);
                if (binding.PreserveAlpha) c.a = binding.Target.color.a;
                binding.Target.color = c;
            }
        }

        private void ApplyOutlines(UITheme theme)
        {
            if (_outlines == null) return;

            for (var i = 0; i < _outlines.Length; i++)
            {
                var binding = _outlines[i];
                if (binding == null || binding.Target == null) continue;

                var c = theme.Get(binding.Color);
                if (binding.PreserveAlpha) c.a = binding.Target.effectColor.a;
                binding.Target.effectColor = c;
            }
        }

        private void ApplySelectables(UITheme theme)
        {
            if (_selectables == null) return;

            for (var i = 0; i < _selectables.Length; i++)
            {
                var binding = _selectables[i];
                if (binding == null || binding.Target == null) continue;

                var block = binding.Target.colors;

                block.normalColor = ResolveSelectableColor(theme, binding.Target, binding.Normal, binding.PreserveAlpha);
                block.highlightedColor = ResolveSelectableColor(theme, binding.Target, binding.Highlighted, binding.PreserveAlpha);
                block.pressedColor = ResolveSelectableColor(theme, binding.Target, binding.Pressed, binding.PreserveAlpha);
                block.selectedColor = ResolveSelectableColor(theme, binding.Target, binding.Selected, binding.PreserveAlpha);

                var disabled = ResolveSelectableColor(theme, binding.Target, binding.Disabled, binding.PreserveAlpha);
                disabled.a *= Mathf.Clamp01(binding.DisabledAlpha);
                block.disabledColor = disabled;

                binding.Target.colors = block;
            }
        }

        private static Color ResolveSelectableColor(UITheme theme, Selectable selectable, UIThemeColor colorId, bool preserveAlpha)
        {
            var c = theme.Get(colorId);
            if (!preserveAlpha) return c;

            var targetGraphic = selectable.targetGraphic;
            if (targetGraphic == null) return c;

            c.a = targetGraphic.color.a;
            return c;
        }
    }
}
