using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace AIWarsIdle.UI.Common
{
    public sealed class HoldTooltipTrigger : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        private Action _onHoldStart;
        private Action _onHoldEnd;

        public void Bind(Action onHoldStart, Action onHoldEnd)
        {
            _onHoldStart = onHoldStart;
            _onHoldEnd = onHoldEnd;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            _onHoldStart?.Invoke();
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            _onHoldEnd?.Invoke();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _onHoldEnd?.Invoke();
        }
    }
}
