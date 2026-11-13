using Malang.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Malang.UI
{
    /// <summary>
    /// 개인 보정 화면.
    /// M1: 안내 문구와 "다음" 버튼만 있다. M5 에서 휴식·최대 측정 흐름을 붙인다.
    /// </summary>
    public class CalibrationScreen : ScreenBase
    {
        [SerializeField] private Button doneButton;

        public override ScreenId Id => ScreenId.Calibration;

        private void Awake()
        {
            if (doneButton != null)
                doneButton.onClick.AddListener(OnDone);
        }

        private void OnDestroy()
        {
            if (doneButton != null)
                doneButton.onClick.RemoveListener(OnDone);
        }

        private void OnDone()
        {
            Go(ScreenId.Main);
        }
    }
}
