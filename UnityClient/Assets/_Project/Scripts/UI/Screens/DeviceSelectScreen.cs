using Malang.Core;
using Malang.Input;
using UnityEngine;
using UnityEngine.UI;

namespace Malang.UI
{
    /// <summary>
    /// 기기 선택 ("말랑이 연결하기").
    /// 처음 실행할 때, 또는 보호자 모드 › 입력 방식에서 연다. 고른 기기는 저장되어 다음부터 자동 연결.
    /// - 블루투스: 구현 전이라 비활성
    /// - 테스트용 모의 입력
    /// - 개발용 USB 센서(UDP 브리지): 모바일 빌드에서는 숨김
    /// </summary>
    public class DeviceSelectScreen : ScreenBase
    {
        [SerializeField] private Button mockButton;
        [SerializeField] private Button udpButton;
        [SerializeField] private Button bleButton;
        [SerializeField] private CanvasGroup bleGroup;
        [SerializeField] private GameObject udpRoot;

        private const float DisabledAlpha = 0.5f;

        public override ScreenId Id => ScreenId.DeviceSelect;

        private void Awake()
        {
            mockButton.onClick.AddListener(SelectMock);
            udpButton.onClick.AddListener(SelectUdp);
            bleButton.onClick.AddListener(SelectBle);
        }

        private void OnDestroy()
        {
            mockButton.onClick.RemoveListener(SelectMock);
            udpButton.onClick.RemoveListener(SelectUdp);
            bleButton.onClick.RemoveListener(SelectBle);
        }

        public override void OnEnter()
        {
            bool ble = GripSource.IsAvailable(DeviceKind.Ble);
            bleButton.interactable = ble;
            bleGroup.alpha = ble ? 1f : DisabledAlpha;
            udpRoot.SetActive(GripSource.IsAvailable(DeviceKind.Udp));
        }

        private void SelectMock()
        {
            Select(DeviceKind.Mock);
        }

        private void SelectUdp()
        {
            Select(DeviceKind.Udp);
        }

        private void SelectBle()
        {
            Select(DeviceKind.Ble);
        }

        private void Select(DeviceKind kind)
        {
            GripSource source = GripSource.Instance;
            if (source == null || !GripSource.IsAvailable(kind))
                return;

            source.Select(kind, true);
            // M5: 센서이고 보정이 필요하면 보정 화면으로
            Go(ScreenId.Main);
        }
    }
}
