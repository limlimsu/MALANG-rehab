using Malang.Core;
using Malang.Input;
using Malang.Training;
using UnityEngine;
using UnityEngine.UI;

namespace Malang.UI
{
    /// <summary>
    /// 실제 FSR 4개의 휴식값(2초)과 최대 쥐기값(3초씩 3회)을 측정한다.
    /// 측정이 끝나야 세션과 1단계를 시작한다. 모의 입력은 물리 보정을 건너뛴다.
    /// </summary>
    public class CalibrationScreen : ScreenBase
    {
        private const int ChannelCount = 4;
        private const int TrialCount = 3;
        private const int MinimumSamples = 5;
        private enum Step { Idle, Rest, Max, Ready }

        [SerializeField] private Button actionButton;
        [SerializeField] private Button backButton;
        [SerializeField] private LocText actionLabel;
        [SerializeField] private LocText instruction;
        [SerializeField] private LocText status;
        [SerializeField] private LocText[] sensorLabels;
        [SerializeField] private Image[] sensorFills;

        private readonly float[] rest = new float[ChannelCount];
        private readonly float[] sum = new float[ChannelCount];
        private readonly float[] sumSquare = new float[ChannelCount];
        private readonly float[] low = new float[ChannelCount];
        private readonly float[] high = new float[ChannelCount];
        private readonly float[,] peaks = new float[ChannelCount, TrialCount];
        private Step step;
        private float startedAt;
        private float nextDisplayAt;
        private int samples;
        private int lastDeviceSample;
        private int trial;
        private string messageKey;

        public override ScreenId Id => ScreenId.Calibration;

        private void Awake()
        {
            if (actionButton != null) actionButton.onClick.AddListener(OnAction);
            if (backButton != null) backButton.onClick.AddListener(OnBack);
            Loc.Changed += Refresh;
        }

        private void OnDestroy()
        {
            if (actionButton != null) actionButton.onClick.RemoveListener(OnAction);
            if (backButton != null) backButton.onClick.RemoveListener(OnBack);
            Loc.Changed -= Refresh;
        }

        public override void OnEnter()
        {
            GripSource source = GripSource.Instance;
            if (source != null) source.ClearCalibration(); // 새 훈련에는 이전 사람의 기준을 쓰지 않는다.
            step = Step.Idle;
            trial = 0;
            messageKey = null;
            nextDisplayAt = 0f;
            Refresh();
        }

        private void Update()
        {
            GripSource source = GripSource.Instance;
            bool connected = source != null && source.Kind == DeviceKind.Udp &&
                             source.IsConnected && source.RawChannels.Length == ChannelCount;
            if (!connected)
            {
                if (step == Step.Rest || step == Step.Max || step == Step.Ready)
                {
                    step = Step.Idle;
                    trial = 0;
                    source?.ClearCalibration();
                    messageKey = "cal.disconnected";
                }
                if (Time.unscaledTime >= nextDisplayAt)
                {
                    nextDisplayAt = Time.unscaledTime + 0.1f;
                    Refresh();
                }
                return;
            }

            if (step == Step.Rest || step == Step.Max)
            {
                int deviceSample = source.Device.SampleCount;
                if (deviceSample != lastDeviceSample)
                {
                    lastDeviceSample = deviceSample;
                    samples++;
                    int[] raw = source.RawChannels;
                    for (int i = 0; i < ChannelCount; i++)
                    {
                        if (step == Step.Rest)
                        {
                            sum[i] += raw[i];
                            sumSquare[i] += raw[i] * raw[i];
                        }
                        else
                        {
                            low[i] = Mathf.Min(low[i], raw[i]);
                            high[i] = Mathf.Max(high[i], raw[i]);
                        }
                    }
                }

                float duration = step == Step.Rest ? Config.calRestSeconds : Config.calMaxWindowSeconds;
                if (Time.unscaledTime - startedAt >= duration)
                {
                    if (step == Step.Rest) FinishRest();
                    else FinishMax();
                }
            }

            if (Time.unscaledTime >= nextDisplayAt)
            {
                nextDisplayAt = Time.unscaledTime + 0.1f;
                Refresh();
                ShowSensors(source.RawChannels);
            }
        }

        private void OnAction()
        {
            GripSource source = GripSource.Instance;
            if (source == null || source.Kind == DeviceKind.None)
            {
                Go(ScreenId.DeviceSelect);
                return;
            }
            if (source.Kind == DeviceKind.Mock)
            {
                SessionManager.Begin();
                Go(ScreenId.Stage1);
                return;
            }
            if (!source.IsConnected || source.RawChannels.Length != ChannelCount)
            {
                messageKey = "cal.disconnected";
                Refresh();
                return;
            }
            if (step == Step.Ready)
            {
                SessionManager.Begin();
                Go(ScreenId.Stage1);
                return;
            }
            if (step != Step.Idle) return;

            messageKey = null;
            samples = 0;
            lastDeviceSample = source.Device.SampleCount;
            startedAt = Time.unscaledTime;
            if (trial == 0)
            {
                step = Step.Rest;
                for (int i = 0; i < ChannelCount; i++) sum[i] = sumSquare[i] = 0f;
            }
            else
            {
                step = Step.Max;
                for (int i = 0; i < ChannelCount; i++)
                    low[i] = high[i] = source.RawChannels[i];
            }
            Refresh();
        }

        private void FinishRest()
        {
            step = Step.Idle;
            if (samples < MinimumSamples)
            {
                messageKey = "cal.fewSamples";
                Refresh();
                return;
            }
            for (int i = 0; i < ChannelCount; i++)
            {
                float mean = sum[i] / samples;
                float sd = Mathf.Sqrt(Mathf.Max(0f, sumSquare[i] / samples - mean * mean));
                if (sd > Config.calRestStableSD)
                {
                    messageKey = "cal.unstable";
                    Refresh();
                    return;
                }
                rest[i] = mean;
            }
            trial = 1;
            messageKey = "cal.restDone";
            Refresh();
        }

        private void FinishMax()
        {
            step = Step.Idle;
            if (samples < MinimumSamples)
            {
                messageKey = "cal.fewSamples";
                Refresh();
                return;
            }
            for (int i = 0; i < ChannelCount; i++)
                peaks[i, trial - 1] =
                    high[i] - rest[i] >= rest[i] - low[i] ? high[i] : low[i];

            if (trial < TrialCount)
            {
                trial++;
                messageKey = "cal.trialDone";
                Refresh();
                return;
            }

            var max = new float[ChannelCount];
            for (int i = 0; i < ChannelCount; i++)
                max[i] = Calibrator.Aggregate(
                    new[] { peaks[i, 0], peaks[i, 1], peaks[i, 2] }, MaxAggregate.Median);

            GripSource source = GripSource.Instance;
            if (source == null || !source.SetChannelCalibration(rest, max, Config.calMinRange / ChannelCount))
            {
                trial = 0;
                messageKey = "cal.weak";
                Refresh();
                return;
            }
            step = Step.Ready;
            messageKey = "cal.ready";
            Refresh();
        }

        private void OnBack() => Go(ScreenId.Main);

        private void Refresh()
        {
            if (actionButton == null) return;
            GripSource source = GripSource.Instance;
            bool mock = source != null && source.Kind == DeviceKind.Mock;
            bool connected = source != null && source.IsConnected &&
                             source.RawChannels.Length == ChannelCount;

            actionButton.interactable = step != Step.Rest && step != Step.Max;
            actionLabel.Key = source == null || source.Kind == DeviceKind.None ? "cal.chooseDevice" :
                mock ? "cal.mockStart" : step == Step.Ready ? "cal.startGame" :
                trial == 0 ? "cal.recordRest" : "cal.recordMax";
            instruction.Key = mock ? "cal.mockInfo" : step == Step.Rest ? "cal.rest" :
                step == Step.Max ? "cal.squeeze" : step == Step.Ready ? "cal.ready" :
                trial == 0 ? "cal.restPrompt" : "cal.maxPrompt";

            if (status == null) return;
            string info = source == null || source.Kind == DeviceKind.None ? Loc.T("cal.noDevice") :
                mock ? Loc.T("cal.mockInfo") : !connected ? Loc.T("cal.disconnected") :
                step == Step.Rest || step == Step.Max
                    ? Loc.F("cal.measuring", Mathf.CeilToInt(Mathf.Max(0f,
                        (step == Step.Rest ? Config.calRestSeconds : Config.calMaxWindowSeconds) -
                        (Time.unscaledTime - startedAt))))
                    : trial > 0 && step != Step.Ready ? Loc.F("cal.trial", trial, TrialCount) :
                    Loc.T("cal.connected");
            if (messageKey != null && connected && step == Step.Idle)
                info = Loc.T(messageKey) + "  ·  " + info;
            status.SetRaw(info);
        }

        private void ShowSensors(int[] raw)
        {
            if (sensorLabels == null || sensorFills == null) return;
            for (int i = 0; i < ChannelCount && i < sensorLabels.Length && i < sensorFills.Length; i++)
            {
                if (sensorLabels[i] != null)
                    sensorLabels[i].SetRaw(Loc.F("cal.sensor", i + 1, raw[i]));
                if (sensorFills[i] != null)
                {
                    Calibrator calibration = GripSource.Instance.Calibration;
                    sensorFills[i].fillAmount = calibration.HasChannelCalibration
                        ? calibration.NormalizeChannel(i, raw[i])
                        : trial > 0
                            ? Mathf.Clamp01(Mathf.Abs(raw[i] - rest[i]) /
                                             Mathf.Max(1f, Config.rawChannelMax * 0.25f))
                            : 0f;
                }
            }
        }
    }
}
