using System;
using Malang.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Malang.UI
{
    /// <summary>
    /// 정해진 시간 동안 누르고 있어야 발동하는 버튼(보호자 모드 자물쇠).
    /// 짧게 누르면 아무 일도 없고, 누르는 동안 진행 링이 찬다.
    /// </summary>
    public class LongPressButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        [SerializeField] private Image progressRing;

        private bool pressing;
        private float heldSeconds;

        public event Action LongPressed;

        private void OnDisable()
        {
            Cancel();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            pressing = true;
            heldSeconds = 0f;
            SetProgress(0f);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            Cancel();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            Cancel();
        }

        private void Update()
        {
            if (!pressing)
                return;

            float duration = Mathf.Max(0.01f, GameConfig.Get().guardianHoldSeconds);
            heldSeconds += Time.unscaledDeltaTime;
            SetProgress(heldSeconds / duration);

            if (heldSeconds >= duration)
            {
                Cancel();
                LongPressed?.Invoke();
            }
        }

        private void Cancel()
        {
            pressing = false;
            heldSeconds = 0f;
            SetProgress(0f);
        }

        private void SetProgress(float t)
        {
            if (progressRing == null)
                return;

            progressRing.fillAmount = Mathf.Clamp01(t);
            progressRing.enabled = t > 0f;
        }
    }
}
