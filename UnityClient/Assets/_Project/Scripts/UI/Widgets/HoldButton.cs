using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Malang.UI
{
    /// <summary>누르고 있는 동안만 켜지는 버튼 (화면 세기 버튼). 손가락이 버튼 밖으로 나가면 뗀 것으로 본다.</summary>
    public class HoldButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        [SerializeField] private Image background;
        [SerializeField] private Color normalColor = Color.white;
        [SerializeField] private Color heldColor = Color.white;

        private bool held;

        public bool IsHeld => held;

        public event Action<bool> HeldChanged;

        public void OnPointerDown(PointerEventData eventData)
        {
            SetHeld(true);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            SetHeld(false);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            SetHeld(false);
        }

        private void OnDisable()
        {
            SetHeld(false);
        }

        private void SetHeld(bool value)
        {
            if (held == value)
                return;

            held = value;
            if (background != null)
                background.color = held ? heldColor : normalColor;
            HeldChanged?.Invoke(held);
        }
    }
}
