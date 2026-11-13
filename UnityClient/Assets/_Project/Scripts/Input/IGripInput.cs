using Malang.Core;

namespace Malang.Input
{
    /// <summary>
    /// 모든 게임은 이 값만 본다. (SPEC §4 변경 금지 계약)
    /// </summary>
    public interface IGripInput
    {
        /// <summary>0–100, 보정·필터 적용 후 개인 상대 힘.</summary>
        float Grip { get; }

        bool IsConnected { get; }

        /// <summary>보정 전 채널 합(디버그·보정용).</summary>
        float RawSum { get; }

        /// <summary>채널별 원시값(없으면 빈 배열).</summary>
        int[] RawChannels { get; }
    }

    public enum DeviceKind
    {
        None,
        Mock,
        Udp,
        Ble
    }

    /// <summary>
    /// 원시 입력 장치. GripSource 가 매 프레임 Tick 하고, 필터·보정을 거쳐 IGripInput 으로 내보낸다.
    /// 채널 수는 가변(N). 하드코딩 금지.
    /// </summary>
    public interface IGripDevice
    {
        DeviceKind Kind { get; }
        bool IsConnected { get; }

        /// <summary>true 면 Sum 이 이미 0–100 (모의 입력).</summary>
        bool IsNormalized { get; }

        int ChannelCount { get; }

        /// <summary>길이는 ChannelCount 이상. 앞 ChannelCount 개만 유효.</summary>
        int[] Channels { get; }

        float Sum { get; }

        /// <summary>기기 시각(ms). 없으면 0.</summary>
        long DeviceMs { get; }

        /// <summary>새 샘플이 들어올 때마다 증가.</summary>
        int SampleCount { get; }

        float SampleRateHz { get; }

        void Open(GameConfig config);
        void Tick(float deltaTime, double now);
        void Close();
    }
}
