using System;
using Malang.Core;
using UnityEngine;

namespace Malang.Input
{
    /// <summary>
    /// 현재 입력 장치를 보유하고 IGripInput 을 노출하는 싱글턴 (DontDestroyOnLoad).
    /// 처리 순서: 원시 장치 → GripFilter(EMA) → Calibrator(0–100)
    /// 모의 입력은 이미 0–100 이므로 mockBypassCalibration 이면 필터·보정을 건너뛴다.
    ///
    /// 선택한 장치와 보정값은 M2 에서는 PlayerPrefs 에 저장한다 (M4 에서 profile.json 으로 이동).
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public class GripSource : MonoBehaviour, IGripInput
    {
        private const string PrefDevice = "malang.device";
        private const string PrefRest = "malang.cal.rest";
        private const string PrefMax = "malang.cal.max";

        private static readonly int[] NoChannels = new int[0];

        private readonly GripFilter filter = new GripFilter();
        private readonly Calibrator calibrator = new Calibrator();

        private GameConfig config;
        private IGripDevice device;
        private int[] channelView = NoChannels;
        private int lastSampleCount;
        private float grip;
        private float rawSum;
        private float filteredSum;
        private double selectedAt;
        private bool lastConnected;

        public static GripSource Instance { get; private set; }

        /// <summary>게임 코드가 읽는 입력. 아직 없으면 null.</summary>
        public static IGripInput Input => Instance;

        public event Action<bool> ConnectionChanged;
        public event Action DeviceChanged;

        // ---------- IGripInput ----------

        public float Grip => grip;
        public bool IsConnected => device != null && device.IsConnected;
        public float RawSum => rawSum;
        public int[] RawChannels => channelView;

        // ---------- 디버그·보정용 ----------

        public DeviceKind Kind => device != null ? device.Kind : DeviceKind.None;
        public IGripDevice Device => device;
        public MockGripInput Mock => device as MockGripInput;
        public Calibrator Calibration => calibrator;
        public float FilteredSum => filteredSum;
        public float SampleRateHz => device != null ? device.SampleRateHz : 0f;
        public long DeviceMs => device != null ? device.DeviceMs : 0;
        public double SecondsSinceSelect => Time.realtimeSinceStartupAsDouble - selectedAt;

        public static bool IsAvailable(DeviceKind kind)
        {
            switch (kind)
            {
                case DeviceKind.Mock: return true;
                case DeviceKind.Udp: return !Application.isMobilePlatform;
                default: return false; // BLE: 구현 전
            }
        }

        public static DeviceKind SavedKind
        {
            get
            {
                DeviceKind kind;
                string saved = PlayerPrefs.GetString(PrefDevice, string.Empty);
                return Enum.TryParse(saved, out kind) ? kind : DeviceKind.None;
            }
        }

        public static bool HasSavedDevice => IsAvailable(SavedKind);

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            if (transform.parent == null)
                DontDestroyOnLoad(gameObject);

            config = GameConfig.Get();
            LoadCalibration();
        }

        private void OnDestroy()
        {
            CloseDevice();
            if (Instance == this)
                Instance = null;
        }

        private void OnApplicationQuit()
        {
            CloseDevice();
        }

        /// <summary>저장된 장치로 연결. 저장된 게 없거나 이 기기에서 못 쓰면 false.</summary>
        public bool ConnectSaved()
        {
            DeviceKind kind = SavedKind;
            if (!IsAvailable(kind))
                return false;

            Select(kind, false);
            return true;
        }

        public void Select(DeviceKind kind, bool save)
        {
            CloseDevice();

            device = Create(kind);
            if (device != null)
                device.Open(config);

            filter.Reset();
            grip = 0f;
            rawSum = 0f;
            filteredSum = 0f;
            lastSampleCount = 0;
            channelView = NoChannels;
            selectedAt = Time.realtimeSinceStartupAsDouble;

            if (save)
            {
                PlayerPrefs.SetString(PrefDevice, kind.ToString());
                PlayerPrefs.Save();
            }

            DeviceChanged?.Invoke();
        }

        public static void ForgetSavedDevice()
        {
            PlayerPrefs.DeleteKey(PrefDevice);
            PlayerPrefs.Save();
        }

        // ---------- 보정 ----------

        public void SetRest(float rest)
        {
            calibrator.SetRest(rest);
            PlayerPrefs.SetFloat(PrefRest, rest);
            PlayerPrefs.Save();
        }

        public void SetMax(float max)
        {
            calibrator.SetMax(max);
            PlayerPrefs.SetFloat(PrefMax, max);
            PlayerPrefs.Save();
        }

        public void ClearCalibration()
        {
            calibrator.Clear();
            PlayerPrefs.DeleteKey(PrefRest);
            PlayerPrefs.DeleteKey(PrefMax);
            PlayerPrefs.Save();
        }

        private void LoadCalibration()
        {
            if (PlayerPrefs.HasKey(PrefRest))
                calibrator.SetRest(PlayerPrefs.GetFloat(PrefRest));
            if (PlayerPrefs.HasKey(PrefMax))
                calibrator.SetMax(PlayerPrefs.GetFloat(PrefMax));
        }

        // ---------- 매 프레임 ----------

        private void Update()
        {
            if (device == null)
            {
                grip = 0f;
                return;
            }

            device.Tick(Time.unscaledDeltaTime, Time.realtimeSinceStartupAsDouble);
            rawSum = device.Sum;
            CopyChannels();

            bool connected = device.IsConnected;

            if (!connected)
            {
                grip = 0f;
                filter.Reset();
            }
            else if (device.IsNormalized && config.mockBypassCalibration)
            {
                filteredSum = rawSum;
                grip = Mathf.Clamp(rawSum, 0f, 100f);
            }
            else
            {
                // EMA 는 새 샘플이 들어왔을 때만 한 번 적용한다 (센서 주기 기준).
                if (device.SampleCount != lastSampleCount)
                {
                    lastSampleCount = device.SampleCount;
                    filteredSum = filter.Step(rawSum, config.emaAlpha);
                }

                if (calibrator.IsReady)
                    grip = calibrator.Normalize(filteredSum);
                else
                    grip = device.IsNormalized ? Mathf.Clamp(filteredSum, 0f, 100f) : 0f;
            }

            if (connected != lastConnected)
            {
                lastConnected = connected;
                ConnectionChanged?.Invoke(connected);
            }
        }

        private void CopyChannels()
        {
            int count = device.ChannelCount;
            if (channelView.Length != count)
                channelView = count == 0 ? NoChannels : new int[count]; // 채널 수가 바뀔 때만 할당

            if (count > 0)
                Array.Copy(device.Channels, channelView, count);
        }

        private void CloseDevice()
        {
            if (device != null)
            {
                device.Close();
                device = null;
            }
        }

        private static IGripDevice Create(DeviceKind kind)
        {
            switch (kind)
            {
                case DeviceKind.Mock: return new MockGripInput();
                case DeviceKind.Udp: return new UdpGripInput();
                case DeviceKind.Ble: return new BleGripInput();
                default: return null;
            }
        }
    }
}
