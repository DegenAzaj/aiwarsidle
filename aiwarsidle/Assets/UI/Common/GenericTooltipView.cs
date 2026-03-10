using TMPro;
using UnityEngine;

namespace AIWarsIdle.UI.Common
{
    public sealed class GenericTooltipView : MonoBehaviour
    {
        [SerializeField] private TMP_Text _titleText;
        [SerializeField] private TMP_Text _bodyText;

        public void Show(string title, string body)
        {
            if (_titleText != null)
            {
                var hasTitle = !string.IsNullOrWhiteSpace(title);
                _titleText.gameObject.SetActive(hasTitle);
                if (hasTitle)
                {
                    _titleText.text = title;
                }
            }

            if (_bodyText != null)
            {
                _bodyText.text = body ?? string.Empty;
            }

            gameObject.SetActive(true);
            transform.SetAsLastSibling();
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }
    }
}
