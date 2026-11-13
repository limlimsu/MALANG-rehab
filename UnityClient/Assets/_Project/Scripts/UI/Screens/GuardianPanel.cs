using Malang.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Malang.UI
{
    /// <summary>
    /// 보호자 모드 패널(메인 위에 겹쳐 뜸).
    /// M2: 언어, 입력 방식(기기 선택 화면), 보정 다시 하기, 화면 세기 버튼 켜기/끄기, 소리 켜기/끄기, 닫기.
    /// M4 에서 난이도·디버그·초기화(오늘/전체/시연용 프로필)·최근 세션을 추가한다.
    /// </summary>
    public class GuardianPanel : MonoBehaviour
    {
        [SerializeField] private ScreenRouter router;
        [SerializeField] private Button koreanButton;
        [SerializeField] private Button englishButton;
        [SerializeField] private Image koreanBackground;
        [SerializeField] private Image englishBackground;
        [SerializeField] private Button inputButton;
        [SerializeField] private Button recalibrateButton;
        [SerializeField] private Button mockPadButton;
        [SerializeField] private LocText mockPadState;
        [SerializeField] private Button soundButton;
        [SerializeField] private LocText soundState;
        [SerializeField] private Button closeButton;

        public bool IsOpen => gameObject.activeSelf;

        private void Awake()
        {
            koreanButton.onClick.AddListener(SelectKorean);
            englishButton.onClick.AddListener(SelectEnglish);
            inputButton.onClick.AddListener(OpenDeviceSelect);
            recalibrateButton.onClick.AddListener(Recalibrate);
            mockPadButton.onClick.AddListener(ToggleMockPad);
            soundButton.onClick.AddListener(ToggleSound);
            closeButton.onClick.AddListener(Close);
        }

        private void OnDestroy()
        {
            koreanButton.onClick.RemoveListener(SelectKorean);
            englishButton.onClick.RemoveListener(SelectEnglish);
            inputButton.onClick.RemoveListener(OpenDeviceSelect);
            recalibrateButton.onClick.RemoveListener(Recalibrate);
            mockPadButton.onClick.RemoveListener(ToggleMockPad);
            soundButton.onClick.RemoveListener(ToggleSound);
            closeButton.onClick.RemoveListener(Close);
        }

        private void OnEnable()
        {
            Loc.Changed += RefreshLanguageButtons;
            RefreshLanguageButtons();
            RefreshMockPad();
            RefreshSound();
        }

        private void OnDisable()
        {
            Loc.Changed -= RefreshLanguageButtons;
        }

        public void Open()
        {
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
        }

        public void Close()
        {
            gameObject.SetActive(false);
        }

        private void SelectKorean()
        {
            Loc.SetLanguage(Loc.Korean);
        }

        private void SelectEnglish()
        {
            Loc.SetLanguage(Loc.English);
        }

        private void OpenDeviceSelect()
        {
            Close();
            if (router != null)
                router.Show(ScreenId.DeviceSelect);
        }

        private void Recalibrate()
        {
            Close();
            if (router != null)
                router.Show(ScreenId.Calibration);
        }

        private void ToggleMockPad()
        {
            MockPadView.PadEnabled = !MockPadView.PadEnabled;
            RefreshMockPad();
        }

        private void ToggleSound()
        {
            AudioPrefs.SoundEnabled = !AudioPrefs.SoundEnabled;
            RefreshSound();
        }

        private void RefreshSound()
        {
            soundState.Key = AudioPrefs.SoundEnabled ? "guard.on" : "guard.off";
        }

        private void RefreshMockPad()
        {
            mockPadState.Key = MockPadView.PadEnabled ? "guard.on" : "guard.off";
        }

        private void RefreshLanguageButtons()
        {
            koreanBackground.color = Loc.IsEnglish ? Palette.Sky : Palette.Yellow;
            englishBackground.color = Loc.IsEnglish ? Palette.Yellow : Palette.Sky;
        }
    }
}
