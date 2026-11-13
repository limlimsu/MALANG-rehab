using System;
using System.Collections;
using Malang.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Malang.UI
{
    /// <summary>
    /// 단계 완료 카드: 화면을 살짝 어둡게 하고 가운데 카드로 제목 · 아이콘 × 개수 · "다음 재활" 버튼.
    /// 아동 화면이므로 개수(점수)만 보여 주고 임상 수치는 보여 주지 않는다.
    /// </summary>
    public class StageCompletePanel : MonoBehaviour
    {
        private const float PopOvershoot = 1.08f;

        [SerializeField] private CanvasGroup group;
        [SerializeField] private RectTransform card;
        [SerializeField] private LocText title;
        [SerializeField] private LocText count;
        [SerializeField] private Button nextButton;

        private Coroutine popRoutine;

        public event Action NextClicked;

        public bool IsShown => gameObject.activeSelf;

        private void Awake()
        {
            nextButton.onClick.AddListener(OnNext);
        }

        private void OnDestroy()
        {
            nextButton.onClick.RemoveListener(OnNext);
        }

        public void Show(string titleKey, object titleArg, int amount)
        {
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            title.Set(titleKey, titleArg);
            count.SetRaw("× " + IntStrings.Get(amount));
            nextButton.interactable = true;

            if (popRoutine != null)
                StopCoroutine(popRoutine);
            popRoutine = StartCoroutine(Pop());
        }

        public void Hide()
        {
            if (popRoutine != null)
                StopCoroutine(popRoutine);
            popRoutine = null;
            gameObject.SetActive(false);
        }

        private void OnNext()
        {
            nextButton.interactable = false; // 두 번 눌림 방지
            NextClicked?.Invoke();
        }

        /// <summary>어두운 배경이 서서히, 카드는 톡 튀어나와 살짝 커졌다가 제 크기로.</summary>
        private IEnumerator Pop()
        {
            float duration = Mathf.Max(0.01f, GameConfig.Get().stageCompletePopSeconds);

            for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                float k = t / duration;
                group.alpha = Mathf.Clamp01(k * 2f);
                float s = k < 0.7f ? Mathf.Lerp(0.6f, PopOvershoot, k / 0.7f)
                    : Mathf.Lerp(PopOvershoot, 1f, (k - 0.7f) / 0.3f);
                card.localScale = new Vector3(s, s, 1f);
                yield return null;
            }

            group.alpha = 1f;
            card.localScale = Vector3.one;
            popRoutine = null;
        }
    }
}
