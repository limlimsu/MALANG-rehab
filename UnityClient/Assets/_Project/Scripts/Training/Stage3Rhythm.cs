using System;
using System.Collections.Generic;
using Malang.Core;
using UnityEngine;

namespace Malang.Training
{
    public enum NoteResult
    {
        None,
        Good,
        Early,
        Late,
        WrongLevel
    }

    /// <summary>
    /// 3단계 · 리듬 타이밍 — "신호 → 반응" 방식 (사용자 결정으로 SPEC §8.5 레인 방식 대신).
    ///
    /// 박자(s3Bpm)에 맞춘 시간표. 처음 s3LeadInBars 마디는 노래만, 그다음 노트 하나당 s3BarsPerNote(2) 마디:
    ///   1마디째 (카운트다운): 노트 등장, 링이 한 바퀴 차오름 (3 · 2 · 1). 링이 꽉 찬 순간 = "지금"
    ///   2마디째 (버티기)    : 등급 구간 안에 있는 동안만 버티기 게이지가 찬다.
    ///
    /// 판정 (화면에 보이는 것과 1:1, 입력 지연 s3InputLatencyMs 만큼 시각을 앞당겨 계산):
    ///   ① 쥐기 시작 = g 가 s3OnsetThreshold 이상이 된 첫 시각이 "지금" ±판정 창 안.
    ///      창이 열리기 전에 쥐었다가 놓음 임계(s3ReleaseThreshold) 이하로 빼면 없던 일로 친다.
    ///      창이 열릴 때까지 계속 쥐고 있었으면 "빨랐어", 창이 닫힐 때까지 안 쥐면 "늦었어".
    ///   ② 버티기 = 쥐기 시작 뒤, 유지 시간(s3HoldBeats 박)의 s3HoldRequiredFraction 이상을
    ///      등급 구간(±허용 오차) 안에서 보내면 그 즉시 "좋아!". 유지 시간이 끝날 때까지 못 채우면 "힘이 달라요".
    ///   빼기 타이밍은 판정하지 않는다 (힘 빼기 측정은 믿기 어렵다는 결정). 다음 노트 전에 힘만 빼면 된다.
    /// 지표: 정확도(버티기 동안 |g - 등급 중심| 평균), 타이밍 오차(|쥐기 시작 오차| 평균).
    /// </summary>
    public class Stage3Rhythm : StageBase
    {
        public const string MsgGuide = "s3.guide";
        public const string MsgHold = "s3.hold";
        public const string MsgMore = "s2.more";
        public const string MsgLess = "s2.less";
        public const string MsgRelax = "s3.relax";
        public const string MsgGood = "s3.good";
        public const string MsgWrongLevel = "s3.wrongLevel";
        public const string MsgLate = "s3.late";
        public const string MsgEarly = "s3.early";
        public static readonly string[] CueKeys = { "s3.cueSoft", "s3.cueNormal", "s3.cueStrong" };
        public static readonly string[] GradeKeys = { "s3.soft", "s3.normal", "s3.strong" };

        public enum NotePhase
        {
            LeadIn,     // 노래만 (첫 노트 전)
            Countdown,  // 노트 등장 ~ "지금"
            Hold,       // "지금" ~ 판정 (버티기)
            Feedback,   // 판정 후 ~ 다음 노트
            Done
        }

        private readonly List<float> accuracySamples = new List<float>();
        private readonly List<float> timingErrors = new List<float>();

        private float beat;
        private float bar;
        private NotePhase phase;
        private int index = -1;
        private double noteStart;
        private double nowAt;
        private double holdEnd;
        private double judgedAt;

        private bool onsetSeen;
        private double onsetAt;
        private bool timingOk;
        private float inBandSeconds;
        private float requiredSeconds;

        public int NoteCount => Cfg.Game.s3NoteCount;
        public int NoteIndex => index;
        public int CurrentGrade { get; private set; }
        public NotePhase Phase => phase;
        public int Successes { get; private set; }
        public int JudgedCount { get; private set; }
        public NoteResult LastResult { get; private set; }
        public float BeatSeconds => beat;

        /// <summary>지금 등급 구간 안에 있는지 (버티기 중).</summary>
        public bool InBand { get; private set; }

        /// <summary>단계 시작 후 초.</summary>
        public double StageElapsed => Elapsed;

        /// <summary>현재 노트 시작(첫 노트 전이면 단계 시작)부터 몇 번째 박인지 (0부터).</summary>
        public int BeatIndex
        {
            get
            {
                double from = index < 0 ? 0d : noteStart;
                return (int)Math.Floor((Elapsed - from) / beat);
            }
        }

        /// <summary>카운트다운 링 0–1 (노트 등장 ~ "지금"). 꽉 찬 순간이 "지금".</summary>
        public float CountdownProgress01 =>
            phase == NotePhase.Countdown ? Mathf.Clamp01((float)((Elapsed - noteStart) / (nowAt - noteStart))) : 0f;

        /// <summary>버티기 게이지 0–1 (등급 구간 안에 있던 시간 / 필요한 시간).</summary>
        public float MeterProgress01 => requiredSeconds > 0f ? Mathf.Clamp01(inBandSeconds / requiredSeconds) : 0f;

        /// <summary>노트 등장 (인자: 노트 번호, 등급 0 살짝 / 1 보통 / 2 세게).</summary>
        public event Action<int, int> NoteAppeared;

        /// <summary>"지금" 순간 (인자: 노트 번호).</summary>
        public event Action<int> NowReached;

        /// <summary>노트 판정 (인자: 노트 번호, 결과).</summary>
        public event Action<int, NoteResult> NoteJudged;

        // ---------- 시간표 (자가 테스트·화면용) ----------

        public double NoteStartOf(int k)
        {
            return (Cfg.Game.s3LeadInBars + k * Cfg.Game.s3BarsPerNote) * (double)bar;
        }

        public double NowOf(int k)
        {
            return NoteStartOf(k) + bar;
        }

        public double HoldEndOf(int k)
        {
            return NowOf(k) + Cfg.Game.s3HoldBeats * (double)beat;
        }

        public int GradeOf(int k)
        {
            int[] pattern = Cfg.Game.s3Pattern;
            if (pattern == null || pattern.Length == 0)
                return 0;
            return Mathf.Clamp(pattern[k % pattern.Length], 0, 2);
        }

        public float GradeMin(int grade)
        {
            GameConfig g = Cfg.Game;
            return grade == 0 ? g.s3SoftMin : grade == 1 ? g.s3NormalMin : g.s3StrongMin;
        }

        public float GradeMax(int grade)
        {
            GameConfig g = Cfg.Game;
            return grade == 0 ? g.s3SoftMax : grade == 1 ? g.s3NormalMax : g.s3StrongMax;
        }

        // ---------- 진행 ----------

        protected override void OnBegin()
        {
            GameConfig g = Cfg.Game;
            beat = 60f / Mathf.Max(1f, g.s3Bpm);
            bar = beat * Mathf.Max(1, g.s3BeatsPerBar);
            accuracySamples.Clear();
            timingErrors.Clear();
            index = -1;
            phase = NotePhase.LeadIn;
            Successes = 0;
            JudgedCount = 0;
            LastResult = NoteResult.None;
            InBand = false;
            SetMessage(MsgGuide);
        }

        protected override void OnStart(double t)
        {
        }

        protected override void OnTick(float grip, double t)
        {
            if (IsDone)
                return;

            double e = Elapsed;

            if (phase == NotePhase.LeadIn || phase == NotePhase.Feedback)
            {
                int next = index + 1;
                if (next < NoteCount && e >= NoteStartOf(next))
                {
                    StartNote(next);
                }
                else
                {
                    if (phase == NotePhase.Feedback)
                        TickFeedback(grip, e, next >= NoteCount);
                    return;
                }
            }

            TrackNote(grip, e);
        }

        private void StartNote(int k)
        {
            index = k;
            noteStart = NoteStartOf(k);
            nowAt = NowOf(k);
            holdEnd = HoldEndOf(k);
            onsetSeen = false;
            timingOk = false;
            inBandSeconds = 0f;
            requiredSeconds = (float)(holdEnd - nowAt) * Cfg.Game.s3HoldRequiredFraction;
            InBand = false;
            CurrentGrade = GradeOf(k);
            phase = NotePhase.Countdown;
            SetMessage(CueKeys[CurrentGrade]);
            NoteAppeared?.Invoke(k, CurrentGrade);
        }

        private void TrackNote(float g, double e)
        {
            GameConfig cfg = Cfg.Game;
            double window = Cfg.S3WindowMs / 1000d;
            double latency = cfg.s3InputLatencyMs / 1000d;
            double te = e - latency; // 입력 지연 보정한 시각

            if (phase == NotePhase.Countdown && e >= nowAt)
            {
                phase = NotePhase.Hold;
                NowReached?.Invoke(index);
            }

            // ① 쥐기 시작 --------------------------------------------------
            if (!timingOk)
            {
                // 창이 열리기 전에 쥐었다가 다시 뺐으면 없던 일로
                if (onsetSeen && te < nowAt - window && g <= cfg.s3ReleaseThreshold)
                    onsetSeen = false;

                if (!onsetSeen && g >= cfg.s3OnsetThreshold)
                {
                    onsetSeen = true;
                    onsetAt = te;
                }

                if (onsetSeen && te >= nowAt - window)
                {
                    // 창이 열린 뒤: 그 전에 쥐기 시작해서 계속 쥐고 있었으면 빨랐다
                    double error = onsetAt - nowAt;
                    if (error < -window)
                    {
                        Judge(NoteResult.Early, error);
                        return;
                    }

                    if (error <= window)
                    {
                        timingOk = true;
                        timingErrors.Add((float)Math.Abs(error * 1000d));
                    }
                }

                if (!timingOk && te > nowAt + window)
                {
                    Judge(NoteResult.Late, double.NaN);
                    return;
                }

                if (!timingOk)
                    return;
            }

            // ② 버티기 --------------------------------------------------------
            float min = GradeMin(CurrentGrade) - cfg.s3GradeTolerance;
            float max = GradeMax(CurrentGrade) + cfg.s3GradeTolerance;
            InBand = g >= min && g <= max;

            if (InBand)
            {
                inBandSeconds += Dt;
                SetMessage(MsgHold);
            }
            else
            {
                SetMessage(g < min ? MsgMore : MsgLess);
            }

            accuracySamples.Add(Mathf.Abs(g - (GradeMin(CurrentGrade) + GradeMax(CurrentGrade)) * 0.5f));

            if (inBandSeconds >= requiredSeconds)
                Judge(NoteResult.Good, double.NaN);
            else if (e >= holdEnd)
                Judge(NoteResult.WrongLevel, double.NaN);
        }

        private void TickFeedback(float g, double e, bool lastNote)
        {
            // 판정 뒤 한 박이 지나도 계속 쥐고 있으면 힘을 빼라고 안내 (다음 노트가 "빨랐어"가 되지 않게)
            if (!lastNote && g > Cfg.Game.s3ReleaseThreshold && e >= judgedAt + beat)
                SetMessage(MsgRelax);

            // 마지막 노트는 판정 뒤 2박 쉬고 끝
            if (lastNote && e >= judgedAt + 2f * beat)
                Finish();
        }

        private void Judge(NoteResult result, double onsetError)
        {
            JudgedCount++;
            if (result == NoteResult.Good)
                Successes++;

            LastResult = result;
            InBand = false;
            judgedAt = Elapsed;
            phase = NotePhase.Feedback;
            SetMessage(MessageFor(result));
            NoteJudged?.Invoke(index, result);
        }

        public static string MessageFor(NoteResult result)
        {
            switch (result)
            {
                case NoteResult.Good: return MsgGood;
                case NoteResult.Early: return MsgEarly;
                case NoteResult.WrongLevel: return MsgWrongLevel;
                default: return MsgLate;
            }
        }

        protected override void Finish()
        {
            IsDone = true;
            phase = NotePhase.Done;
        }

        protected override StageResult BuildResult()
        {
            return new StageResult
            {
                stage = 3,
                tries = JudgedCount,
                successes = Successes,
                score = Successes,
                windowMs = Cfg.S3WindowMs,
                accuracyMae = Mean(accuracySamples),
                timingErrMs = Mean(timingErrors)
            };
        }

        private static float Mean(List<float> values)
        {
            if (values.Count == 0)
                return StageResult.None;

            float sum = 0f;
            for (int i = 0; i < values.Count; i++)
                sum += values[i];
            return sum / values.Count;
        }
    }
}
