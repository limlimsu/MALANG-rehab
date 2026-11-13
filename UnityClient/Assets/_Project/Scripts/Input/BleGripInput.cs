using Malang.Core;
using UnityEngine;

namespace Malang.Input
{
    /// <summary>
    /// 블루투스(BLE) 입력 — 뼈대만 (SPEC §5.3).
    ///
    /// TODO(BLE):
    ///  - Android 네이티브 플러그인(Kotlin)으로 스캔·연결·Notify 구독. Unity 에는 BLE API 가 없다.
    ///  - 패킷(예정, 50 Hz Notify): seq uint16, t_ms uint32, ch[N] uint16 (+ 배터리, 펌웨어 버전 추가 검토)
    ///  - seq 로 손실 감지, 플러그인 콜백 스레드 → 메인 스레드는 UdpGripInput 처럼 최신 값만 전달
    ///  - 기기 ID 저장 후 자동 재연결, 끊김 시 IsConnected = false
    /// </summary>
    public class BleGripInput : IGripDevice
    {
        private static readonly int[] NoChannels = new int[0];

        public DeviceKind Kind => DeviceKind.Ble;
        public bool IsConnected => false;
        public bool IsNormalized => false;
        public int ChannelCount => 0;
        public int[] Channels => NoChannels;
        public float Sum => 0f;
        public long DeviceMs => 0;
        public int SampleCount => 0;
        public float SampleRateHz => 0f;

        public void Open(GameConfig config)
        {
            Debug.LogWarning("BleGripInput: 아직 구현되지 않았습니다(뼈대).");
        }

        public void Tick(float deltaTime, double now)
        {
        }

        public void Close()
        {
        }
    }
}
