using System;
using System.Collections.Generic;
using Malang.Core;
using UnityEngine;

namespace Malang.Training
{
    /// <summary>
    /// 1단계 · 몬스터 격파 — 쥐기–놓기 전환, 반응 (SPEC §8.3 + 대기 UX 보강).
    /// 1. 몬스터 등장(t_spawn) → 쥠(g ≥ 50) 시 피격, 처치 +1, 반응시간 = t_grip - t_spawn
    /// 2. 처치 후 몬스터 사라짐, "좋아! 이제 힘을 쭉 빼봐"
    /// 3. 놓음(g ≤ 20) 확인 → 쉬기:
    ///    힘을 뺀 상태(g ≤ 20)를 0.6–1.4초(무작위) 동안 계속 유지해야 다음으로 넘어간다.
    ///    도중에 힘이 20%를 넘으면 처음부터 다시 기다리고 "힘을 빼고 기다려요".
    /// 4. 예고: 덤불이 흔들리며 "!" (s1TelegraphSeconds). 예고 중 힘을 주면 취소하고 다시 쉬기.
    /// 5. 몬스터 등장 → "다시 꾹!"
    ///    안전장치: 쉬기를 s1RestMaxWaitSeconds 넘게 못 마치면 예고·등장을 강제로 진행.
    ///    이미 쥔 채로 나왔으면 "힘을 뺐다가 다시 꾹!" (놓았다가 다시 쥐어야 처치).
    /// 6. 쥔 채로 2초가 지나면 "힘을 빼야 다음 몬스터가 나와!"
    /// 7. 10마리 또는 제한 시간 종료 → 완료
    /// 시행 1회 = 몬스터 1마리. 성공 = 등장 후 3초 안에 쥠 + 이후 완전 놓음.
    /// </summary>
    public class Stage1Monster : StageBase
    {
        public const string MsgAppear = "s1.appear";
        public const string MsgRelax = "s1.relax";
        public const string MsgWait = "s1.wait";
        public const string MsgRestAgain = "s1.rest";
        public const string MsgAgain = "s1.again";
        public const string MsgReleaseFirst = "s1.releaseFirst";
        public const string MsgHoldTooLong = "s1.holdTooLong";
        public const string MsgClear = "s1.clear";
        public const string MsgPartial = "s1.partial";

        private enum Phase
        {
            Waiting,     // 몬스터가 나와 쥐기를 기다림
            Hit,         // 처치 후 놓기를 기다림
            Resting,     // 힘을 뺀 상태를 유지하며 다음 몬스터를 기다림
            Telegraph,   // 등장 예고 (덤불 흔들림 + "!")
            Idle         // 끝남
        }

        private readonly List<float> reactionsMs = new List<float>();
        private HysteresisDetector detector;
        private Phase phase;
        private double spawnAt;
        private double hitAt;
        private double restBegan;      // 쉬기 단계 시작 (안전장치 기준)
        private double relaxedSince;   // 힘을 뺀 상태가 이어지기 시작한 시각
        private float restNeeded;      // 이번에 필요한 연속 쉬기 시간
        private double telegraphEnd;
        private bool telegraphForced;
        private bool hitInWindow;
        private bool holdWarned;
        private bool timeUp;

        public int Kills { get; private set; }
        public int Tries { get; private set; }
        public int Successes { get; private set; }
        public int Repetitions { get; private set; }
        public int TargetKills => Cfg.Game.s1TargetKills;
        public bool IsGripped => detector != null && detector.IsGripped;

        /// <summary>몬스터가 나와서 쥐기를 기다리는 중.</summary>
        public bool MonsterOut => phase == Phase.Waiting;

        /// <summary>몬스터가 나왔지만 이미 쥐고 있어서, 먼저 힘을 빼야 하는 상태.</summary>
        public bool BlockedByPreGrip => phase == Phase.Waiting && detector.IsGripped;

        /// <summary>힘을 빼야 하는 단계 (처치 후 놓기, 쉬기, 예고).</summary>
        public bool NeedsRelease => phase == Phase.Hit || phase == Phase.Resting || phase == Phase.Telegraph;

        public bool IsTelegraphing => phase == Phase.Telegraph;

        public float RemainingSeconds =>
            timeUp ? 0f : Mathf.Max(0f, Cfg.Game.s1TimeLimitSeconds - (float)Elapsed);

        /// <summary>몬스터가 나타남 (화면: 등장 애니메이션).</summary>
        public event Action Spawned;

        /// <summary>몬스터를 쥐어서 처치함 (화면: 피격 후 쓰러짐).</summary>
        public event Action Hit;

        /// <summary>등장 예고 시작 (화면: 덤불 흔들림 + "!").</summary>
        public event Action TelegraphStarted;

        /// <summary>예고 중 힘을 줘서 예고 취소 (화면: 덤불·"!" 원래대로).</summary>
        public event Action TelegraphCanceled;

        protected override void OnBegin()
        {
            GameConfig g = Cfg.Game;
            detector = new HysteresisDetector(g.gripThreshold, g.releaseThreshold);
            reactionsMs.Clear();
            Kills = 0;
            Tries = 0;
            Successes = 0;
            Repetitions = 0;
            timeUp = false;
            phase = Phase.Idle;
            SetMessage(MsgAppear);
        }

        protected override void OnStart(double t)
        {
            Spawn(t);
        }

        protected override void OnTick(float grip, double t)
        {
            GripTransition transition = detector.Update(grip, t);
            GameConfig g = Cfg.Game;

            if (!IsDone && Elapsed >= g.s1TimeLimitSeconds)
            {
                timeUp = true;
                Finish();
            }

            switch (phase)
            {
                case Phase.Waiting:
                    if (IsDone)
                        break;
                    if (transition == GripTransition.Gripped)
                        OnMonsterGripped(t);
                    else if (transition == GripTransition.Released)
                        SetMessage(MsgAgain); // 쥔 채로 나왔다가 이제 힘을 뺌 → 다시 쥐면 된다
                    break;

                case Phase.Hit:
                    if (transition == GripTransition.Released)
                    {
                        // 완전히 놓아야 유효 반복 1회. 끝난 뒤의 마지막 놓기도 지표에 넣는다.
                        Repetitions++;
                        if (hitInWindow)
                            Successes++;

                        if (IsDone)
                            phase = Phase.Idle;
                        else
                            BeginRest(t);
                    }
                    else if (!IsDone && !holdWarned && t - hitAt >= g.s1HoldTooLongSeconds)
                    {
                        holdWarned = true;
                        SetMessage(MsgHoldTooLong);
                    }
                    break;

                case Phase.Resting:
                    if (!IsDone)
                        TickRest(grip, t, g);
                    break;

                case Phase.Telegraph:
                    if (IsDone)
                        break;
                    if (!telegraphForced && grip > g.releaseThreshold)
                    {
                        // 예고 중에 힘을 주면 취소하고 다시 쉬기
                        phase = Phase.Resting;
                        relaxedSince = t;
                        SetMessage(MsgRestAgain);
                        TelegraphCanceled?.Invoke();
                    }
                    else if (t >= telegraphEnd)
                    {
                        Spawn(t);
                    }
                    break;
            }
        }

        private void BeginRest(double t)
        {
            GameConfig g = Cfg.Game;
            phase = Phase.Resting;
            restBegan = t;
            relaxedSince = t;
            restNeeded = UnityEngine.Random.Range(g.s1RespawnDelayMin, g.s1RespawnDelayMax);
            SetMessage(MsgWait);
        }

        private void TickRest(float grip, double t, GameConfig g)
        {
            if (grip > g.releaseThreshold)
            {
                relaxedSince = t; // 힘을 주면 연속 쉬기 시간을 처음부터
                SetMessage(MsgRestAgain);
            }
            else
            {
                SetMessage(MsgWait);
            }

            bool rested = grip <= g.releaseThreshold && t - relaxedSince >= restNeeded;
            bool timedOut = g.s1RestMaxWaitSeconds > 0f && t - restBegan >= g.s1RestMaxWaitSeconds;

            if (rested || timedOut)
            {
                phase = Phase.Telegraph;
                telegraphForced = !rested;
                telegraphEnd = t + g.s1TelegraphSeconds;
                TelegraphStarted?.Invoke();
            }
        }

        private void Spawn(double t)
        {
            phase = Phase.Waiting;
            spawnAt = t;

            // 첫 몬스터는 "몬스터가 나타났어! …", 그다음부터는 나타나는 순간 "다시 꾹!"
            // 안전장치로 쥔 채 나왔으면 먼저 힘을 빼라고 안내
            if (Tries > 0)
                SetMessage(detector.IsGripped ? MsgReleaseFirst : MsgAgain);

            Tries++;
            Spawned?.Invoke();
        }

        private void OnMonsterGripped(double t)
        {
            Kills++;
            float reaction = (float)(t - spawnAt);
            reactionsMs.Add(reaction * 1000f);
            hitInWindow = reaction <= Cfg.Game.s1SuccessGripWindowSeconds;
            hitAt = t;
            holdWarned = false;
            phase = Phase.Hit;
            Hit?.Invoke();

            if (Kills >= TargetKills)
                Finish();
            else
                SetMessage(MsgRelax);
        }

        protected override void Finish()
        {
            IsDone = true;
            if (phase != Phase.Hit)
                phase = Phase.Idle;

            if (Kills >= TargetKills)
                SetMessage(MsgClear);
            else
                SetMessage(MsgPartial, Kills);
        }

        protected override StageResult BuildResult()
        {
            float mean = StageResult.None;
            if (reactionsMs.Count > 0)
            {
                float sum = 0f;
                for (int i = 0; i < reactionsMs.Count; i++)
                    sum += reactionsMs[i];
                mean = sum / reactionsMs.Count;
            }

            return new StageResult
            {
                stage = 1,
                tries = Tries,
                successes = Successes,
                score = Kills,
                reactionMsMean = mean,
                repetitions = Repetitions
            };
        }
    }
}
