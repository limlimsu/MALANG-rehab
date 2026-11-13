using Malang.Core;
using Malang.Input;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Malang.UI
{
    /// <summary>
    /// 디버그 오버레이 (F1). 입력 방식, 연결 상태, 수신 주기, RawSum, 채널값, g, 보정값, FPS.
    /// - 슬라이더: 모의 입력 값을 직접 지정 (끄는 동안 모의 입력 계산 정지)
    /// - 휴식값/최대값 기록: 보정 화면(M5) 전까지 센서를 바로 쓰기 위한 개발용 빠른 보정
    /// 글자는 debugRefreshHz 로만 갱신하고, 문자열을 만들지 않고 char 배열에 직접 쓴다(GC 없음).
    /// </summary>
    public class DebugOverlay : MonoBehaviour
    {
        private const int BufferLength = 1024;
        private const float FpsSmoothing = 0.1f;

        [SerializeField] private GameObject panel;
        [SerializeField] private TMP_Text body;
        [SerializeField] private Slider slider;
        [SerializeField] private SliderDrag sliderDrag;
        [SerializeField] private Button restButton;
        [SerializeField] private Button maxButton;

        private readonly char[] buffer = new char[BufferLength];
        private int length;
        private float nextRefresh;
        private float fps;

        private string lSource, lConnected, lYes, lNo, lRate, lRaw, lChannels, lGrip, lGoal, lCalibration,
            lRest, lMax, lNone, lFps, lMock, lUdp, lBle;

        public bool IsVisible => panel.activeSelf;

        private void Awake()
        {
            slider.minValue = 0f;
            slider.maxValue = 100f;
            slider.onValueChanged.AddListener(OnSliderChanged);
            sliderDrag.DraggingChanged += OnDraggingChanged;
            restButton.onClick.AddListener(RecordRest);
            maxButton.onClick.AddListener(RecordMax);
        }

        private void OnDestroy()
        {
            slider.onValueChanged.RemoveListener(OnSliderChanged);
            sliderDrag.DraggingChanged -= OnDraggingChanged;
            restButton.onClick.RemoveListener(RecordRest);
            maxButton.onClick.RemoveListener(RecordMax);
        }

        private void OnEnable()
        {
            Loc.Changed += CacheLabels;
            CacheLabels();
        }

        private void OnDisable()
        {
            Loc.Changed -= CacheLabels;
        }

        public void Toggle()
        {
            panel.SetActive(!panel.activeSelf);
            nextRefresh = 0f;
        }

        private void Update()
        {
            if (!panel.activeSelf)
                return;

            float dt = Time.unscaledDeltaTime;
            if (dt > 0f)
                fps = fps <= 0f ? 1f / dt : Mathf.Lerp(fps, 1f / dt, FpsSmoothing);

            GripSource source = GripSource.Instance;
            if (source == null)
                return;

            bool mock = source.Kind == DeviceKind.Mock;
            if (slider.interactable != mock)
                slider.interactable = mock;
            if (!sliderDrag.IsDragging)
                slider.SetValueWithoutNotify(source.Grip);

            bool sensor = source.Kind == DeviceKind.Udp && source.IsConnected;
            if (restButton.interactable != sensor)
                restButton.interactable = sensor;
            if (maxButton.interactable != sensor)
                maxButton.interactable = sensor;

            if (Time.unscaledTime < nextRefresh)
                return;

            nextRefresh = Time.unscaledTime + 1f / Mathf.Max(1f, GameConfig.Get().debugRefreshHz);
            Compose(source);
            body.SetCharArray(buffer, 0, length);
        }

        private void Compose(GripSource source)
        {
            length = 0;

            Put(lSource); Put(": "); Put(KindName(source.Kind)); NewLine();

            Put(lConnected); Put(": "); Put(source.IsConnected ? lYes : lNo);
            if (source.Kind == DeviceKind.Udp)
            {
                Put("   "); Put(lRate); Put(": "); PutFixed1(source.SampleRateHz); Put(" Hz");
            }
            NewLine();

            Put(lRaw); Put(": "); PutInt(Mathf.RoundToInt(source.RawSum));
            int[] channels = source.RawChannels;
            if (channels.Length > 0)
            {
                Put("   "); Put(lChannels); Put(":");
                for (int i = 0; i < channels.Length; i++)
                {
                    Put(" ");
                    PutInt(channels[i]);
                }
            }
            NewLine();

            Put(lGrip); Put(": "); PutFixed1(source.Grip); Put("%");
            MockGripInput mock = source.Mock;
            if (mock != null)
            {
                Put("   "); Put(lGoal); Put(": "); PutInt(Mathf.RoundToInt(mock.CurrentGoal));
            }
            NewLine();

            if (source.Kind == DeviceKind.Udp)
            {
                Calibrator cal = source.Calibration;
                Put(lCalibration); Put(": ");
                Put(lRest); Put(" "); PutCal(cal.HasRest, cal.Rest);
                Put(" / ");
                Put(lMax); Put(" "); PutCal(cal.HasMax, cal.Max);
                NewLine();
            }

            Put(lFps); Put(": "); PutInt(Mathf.RoundToInt(fps));
        }

        private void CacheLabels()
        {
            lSource = Loc.T("dbg.source");
            lConnected = Loc.T("dbg.connected");
            lYes = Loc.T("dbg.yes");
            lNo = Loc.T("dbg.no");
            lRate = Loc.T("dbg.rate");
            lRaw = Loc.T("dbg.raw");
            lChannels = Loc.T("dbg.channels");
            lGrip = Loc.T("dbg.grip");
            lGoal = Loc.T("dbg.goal");
            lCalibration = Loc.T("dbg.calibration");
            lRest = Loc.T("dbg.rest");
            lMax = Loc.T("dbg.max");
            lNone = Loc.T("dbg.none");
            lFps = Loc.T("dbg.fps");
            lMock = Loc.T("guard.inputMock");
            lUdp = Loc.T("guard.inputUdp");
            lBle = Loc.T("guard.inputBle");
            nextRefresh = 0f;
        }

        private string KindName(DeviceKind kind)
        {
            switch (kind)
            {
                case DeviceKind.Mock: return lMock;
                case DeviceKind.Udp: return lUdp;
                case DeviceKind.Ble: return lBle;
                default: return lNone;
            }
        }

        // ---------- 슬라이더 · 빠른 보정 ----------

        private void OnDraggingChanged(bool dragging)
        {
            GripSource source = GripSource.Instance;
            MockGripInput mock = source != null ? source.Mock : null;
            if (mock == null)
                return;

            if (dragging)
                mock.SetOverride(slider.value);
            else
                mock.EndOverride();
        }

        private void OnSliderChanged(float value)
        {
            if (!sliderDrag.IsDragging)
                return;

            GripSource source = GripSource.Instance;
            MockGripInput mock = source != null ? source.Mock : null;
            if (mock != null)
                mock.SetOverride(value);
        }

        private void RecordRest()
        {
            GripSource source = GripSource.Instance;
            if (source != null)
                source.SetRest(source.FilteredSum);
        }

        private void RecordMax()
        {
            GripSource source = GripSource.Instance;
            if (source != null)
                source.SetMax(source.FilteredSum);
        }

        // ---------- char 버퍼 쓰기 (할당 없음) ----------

        private void Put(string s)
        {
            for (int i = 0; i < s.Length && length < BufferLength; i++)
                buffer[length++] = s[i];
        }

        private void PutChar(char c)
        {
            if (length < BufferLength)
                buffer[length++] = c;
        }

        private void NewLine()
        {
            PutChar('\n');
        }

        private void PutInt(int value)
        {
            if (value < 0)
            {
                PutChar('-');
                value = -value;
            }

            int start = length;
            do
            {
                PutChar((char)('0' + value % 10));
                value /= 10;
            } while (value > 0 && length < BufferLength);

            // 뒤집기
            for (int a = start, b = length - 1; a < b; a++, b--)
            {
                char t = buffer[a];
                buffer[a] = buffer[b];
                buffer[b] = t;
            }
        }

        private void PutFixed1(float value)
        {
            int tenths = Mathf.RoundToInt(value * 10f);
            if (tenths < 0)
            {
                PutChar('-');
                tenths = -tenths;
            }

            PutInt(tenths / 10);
            PutChar('.');
            PutChar((char)('0' + tenths % 10));
        }

        private void PutCal(bool has, float value)
        {
            if (has)
                PutInt(Mathf.RoundToInt(value));
            else
                Put(lNone);
        }
    }
}
