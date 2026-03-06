using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AIWarsIdle.UI.Generators
{
    public sealed class PrestigeButtonController : MonoBehaviour
    {
        [Header("Context")]
        [SerializeField] private GeneratorsScreenContext _context;

        [Header("UI")]
        [SerializeField] private Button _button;
        [SerializeField] private TMP_Text _label;

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
            if (_button != null)
            {
                _button.onClick.RemoveListener(OnClicked);
                _button.onClick.AddListener(OnClicked);
            }
            Refresh();
        }

        private void OnDisable()
        {
            if (_button != null) _button.onClick.RemoveListener(OnClicked);
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

            var can = loop.Prestige.CanPrestige();
            if (_button != null) _button.interactable = can;
            if (_label != null) _label.text = can ? "REBOOT" : "REBOOT";
        }

        private void OnClicked()
        {
            var loop = _context != null ? _context.Loop : null;
            if (loop == null) return;

            if (!loop.Prestige.CanPrestige())
            {
                Refresh();
                return;
            }

            // MVP: execute directly. Story 11.3 adds a confirm modal.
            loop.Prestige.ExecutePrestigeSingle();
            Refresh();
        }
    }
}
