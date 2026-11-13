using Malang.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Malang.UI
{
    /// <summary>
    /// 결과 화면. M1: 제목과 "메인으로" 버튼만 있다.
    /// M4 에서 결과 카드·경험치 목록·레벨업을 붙인다.
    /// </summary>
    public class ResultScreen : ScreenBase
    {
        [SerializeField] private Button homeButton;

        public override ScreenId Id => ScreenId.Result;

        private void Awake()
        {
            if (homeButton != null)
                homeButton.onClick.AddListener(OnHome);
        }

        private void OnDestroy()
        {
            if (homeButton != null)
                homeButton.onClick.RemoveListener(OnHome);
        }

        private void OnHome()
        {
            Go(ScreenId.Main);
        }
    }
}
