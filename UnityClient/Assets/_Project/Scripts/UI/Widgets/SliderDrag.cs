using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Malang.UI
{
    /// <summary>슬라이더를 손으로 끌고 있는지 알려 준다 (디버그 오버레이의 모의 입력 슬라이더).</summary>
    public class SliderDrag : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        public bool IsDragging { get; private set; }

        public event Action<bool> DraggingChanged;

        public void OnPointerDown(PointerEventData eventData)
        {
            Set(true);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            Set(false);
        }

        private void OnDisable()
        {
            Set(false);
        }

        private void Set(bool value)
        {
            if (IsDragging == value)
                return;
            IsDragging = value;
            DraggingChanged?.Invoke(value);
        }
    }
}
