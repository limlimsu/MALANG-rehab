using Malang.Core;
using Malang.Input;
using UnityEngine;

namespace Malang.UI
{
    /// <summary>
    /// 입력 상태 표시:
    /// - 모의 입력이면 위쪽 가운데 "테스트 모드" 표시
    /// - 센서가 disconnectTimeoutSeconds 넘게 끊기면 "말랑이 연결이 끊겼어요" 배너
    ///   (게임 일시정지는 M3 에서 GripSource.ConnectionChanged 로 연결)
    /// </summary>
    public class InputStatusView : MonoBehaviour
    {
        [SerializeField] private GameObject testBadge;
        [SerializeField] private GameObject disconnectBanner;

        private void Update()
        {
            GripSource source = GripSource.Instance;
            DeviceKind kind = source != null ? source.Kind : DeviceKind.None;

            bool mock = kind == DeviceKind.Mock;
            bool lost = kind != DeviceKind.None && kind != DeviceKind.Mock && !source.IsConnected &&
                        source.SecondsSinceSelect > GameConfig.Get().disconnectTimeoutSeconds;

            if (testBadge.activeSelf != mock)
                testBadge.SetActive(mock);
            if (disconnectBanner.activeSelf != lost)
                disconnectBanner.SetActive(lost);
        }
    }
}
