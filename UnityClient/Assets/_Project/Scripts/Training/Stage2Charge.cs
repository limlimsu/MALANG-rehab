using System;
using System.Collections.Generic;
using Malang.Core;
using UnityEngine;

namespace Malang.Training
{
    /// <summary>
    /// 2단계 · 파워 차징 — 목표 힘 유지 (SPEC §8.4).
    /// - 구간 안: 진행 링이 차고 불꽃 켜짐 "좋아요, 그대로!" / 아래 "조금 더 꾹!" / 위 "살짝 힘을 빼요"
    /// - 구간 밖으로 잠깐(이탈 유예 300 ms 이내) 나가도 유지 시간 초기화 안 함
    /// - 3초 유지 → 발사(1.6초 연출), 발사 +1, 다음 시행 전 g ≤ 20 확인
    /// - 시행당 20초를 넘기면 실패 처리 후 다음 시행
    /// 시행 1회 = 발사 시도 1회. 성공 = 제한 시간 안에 발사.
    /// 지표: 유지 중 |g - 중심| 평균(정확도), g 표준편차(안정성).
    /// </summary>
    public class Stage2Charge : StageBase
    {
        public const string MsgGuide = "s2.guide";
        public const string MsgMore = "s2.more";
        public const string MsgLess = "s2.less";
        public const string MsgHold = "s2.hold";
        public const string MsgLaunch = "s2.launch";
        public const string MsgRelax = "s2.relax";
        public const string MsgClear = "s2.clear";

        private enum Phase
        {
            Charging,
            Launching,
            WaitRelease,
            Idle
        }

        private readonly List<float> holdSamples = new List<float>();
        private Phase phase;
        private float holdProgress;
        private double lastInBandAt;
        private double trialStart;
        private double launchEnd;

        public int Launches { get; private set; }
        public int Tries { get; private set; }
        public int Successes { get; private set; }
        public int TargetLaunches => Cfg.Game.s2LaunchTarget;
        public float Center => Cfg.Game.s2BandCenter;
        public float Width => Cfg.S2BandWidth;
        public float Low => Center - Width * 0.5f;
        public float High => Center + Width * 0.5f;
        public bool InBand { get; private set; }

        /// <summary>0–1 유지 진행률 (진행 링).</summary>
        public float HoldProgress01 => Mathf.Clamp01(holdProgress / Cfg.Game.s2HoldSeconds);

        /// <summary>남은 유지 초 (링 가운데 숫자).</summary>
        public int HoldSecondsLeft => Mathf.Max(0, Mathf.CeilToInt(Cfg.Game.s2HoldSeconds - holdProgress));

        public bool FlameOn => phase == Phase.Charging && InBand;

        /// <summary>발사 뒤(또는 시행 실패 뒤) 힘을 빼기를 기다리는 중.</summary>
        public bool WaitingForRelease => phase == Phase.WaitRelease;

        /// <summary>발사 (화면: 로켓 발사 연출).</summary>
        public event Action Launched;

        /// <summary>새 시행 시작. 인자 = 시행 번호(1부터). 화면: 발사대가 비어 있으면 새 로켓이 올라온다.</summary>
        public event Action<int> TrialStarted;

        protected override void OnBegin()
        {
            holdSamples.Clear();
            Launches = 0;
            Tries = 0;
            Successes = 0;
            holdProgress = 0f;
            InBand = false;
            phase = Phase.Idle;
            SetMessage(MsgGuide);
        }

        protected override void OnStart(double t)
        {
            StartTrial(t);
        }

        private void StartTrial(double t)
        {
            phase = Phase.Charging;
            trialStart = t;
            holdProgress = 0f;
            lastInBandAt = double.NegativeInfinity;
            Tries++;
            TrialStarted?.Invoke(Tries);
        }

        protected override void OnTick(float grip, double t)
        {
            GameConfig g = Cfg.Game;

            switch (phase)
            {
                case Phase.Charging:
                    if (IsDone)
                        break;
                    TickCharging(grip, t, g);
                    break;

                case Phase.Launching:
                    if (t >= launchEnd)
                    {
                        if (Launches >= TargetLaunches)
                        {
                            Finish();
                        }
                        else
                        {
                            phase = Phase.WaitRelease;
                            SetMessage(MsgRelax);
                        }
                    }
                    break;

                case Phase.WaitRelease:
                    if (!IsDone && grip <= g.releaseThreshold)
                    {
                        StartTrial(t);
                        SetMessage(MsgGuide);
                    }
                    break;
            }
        }

        private void TickCharging(float grip, double t, GameConfig g)
        {
            InBand = grip >= Low && grip <= High;

            if (InBand)
            {
                holdProgress += Dt;
                lastInBandAt = t;
                SetMessage(MsgHold);
            }
            else
            {
                // 이탈 유예: 잠깐 벗어난 건 유지 시간을 지우지 않는다(늘리지도 않음).
                if (t - lastInBandAt > g.s2ExitGraceSeconds)
                    holdProgress = 0f;

                if (grip <= g.releaseThreshold && holdProgress <= 0f)
                    SetMessage(MsgGuide);
                else
                    SetMessage(grip < Low ? MsgMore : MsgLess);
            }

            // 유지 중(유예 포함) 샘플만 정확도·안정성 지표에 쓴다.
            if (holdProgress > 0f)
                holdSamples.Add(grip);

            if (holdProgress >= g.s2HoldSeconds)
            {
                Launches++;
                Successes++;
                InBand = false;
                phase = Phase.Launching;
                launchEnd = t + g.s2LaunchSequenceSeconds;
                SetMessage(MsgLaunch);
                Launched?.Invoke();
            }
            else if (t - trialStart >= g.s2TrialTimeLimitSeconds)
            {
                // 시행 실패: 힘을 뺀 뒤 다음 시행
                InBand = false;
                phase = Phase.WaitRelease;
                SetMessage(MsgRelax);
            }
        }

        protected override void Finish()
        {
            IsDone = true;
            if (phase != Phase.Launching)
                phase = Phase.Idle;
            InBand = false;
            SetMessage(MsgClear, Launches);
        }

        protected override StageResult BuildResult()
        {
            float mae = StageResult.None;
            float sd = StageResult.None;

            if (holdSamples.Count > 0)
            {
                float absSum = 0f;
                float sum = 0f;
                for (int i = 0; i < holdSamples.Count; i++)
                {
                    absSum += Mathf.Abs(holdSamples[i] - Center);
                    sum += holdSamples[i];
                }

                mae = absSum / holdSamples.Count;
                float mean = sum / holdSamples.Count;
                float variance = 0f;
                for (int i = 0; i < holdSamples.Count; i++)
                {
                    float d = holdSamples[i] - mean;
                    variance += d * d;
                }

                sd = Mathf.Sqrt(variance / holdSamples.Count);
            }

            return new StageResult
            {
                stage = 2,
                tries = Tries,
                successes = Successes,
                score = Launches,
                bandWidth = Width,
                accuracyMae = mae,
                holdSd = sd
            };
        }
    }
}
