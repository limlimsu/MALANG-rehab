using System;
using Malang.Core;
using Malang.Training;
using UnityEditor;
using UnityEngine;

namespace Malang.EditorTools
{
    /// <summary>
    /// MALANG > Self Test > Stage Rules : 1·2·3단계 규칙을 정해진 힘 입력 시나리오로 돌려 보고 Console 에 PASS/FAIL 출력.
    /// Play 없이 에디터에서 바로 실행된다 (60 fps 가정).
    /// </summary>
    public static class StageRuleTests
    {
        private const float Dt = 1f / 60f;

        private static int passed;
        private static int failed;

        [MenuItem("MALANG/Self Test/Stage Rules", priority = 60)]
        public static void Run()
        {
            passed = 0;
            failed = 0;
            GameConfig.ClearCache();
            GameConfig cfg = GameConfig.Get();

            Stage1_GripRelease_TenKills(cfg);
            Stage1_HoldWithoutRelease_NoNextMonster(cfg);
            Stage1_NoGrip_TimeUp(cfg);
            Stage1_SqueezeDuringRest_Delays(cfg);
            Stage1_TelegraphCancel(cfg);
            Stage1_MaxWaitFallback(cfg);
            Stage2_HoldInBand_Launches(cfg);
            Stage2_NeedsReleaseBetweenTrials(cfg);
            Stage2_ExitGrace(cfg);
            Stage2_Metrics(cfg);
            Stage3_PerfectPlay(cfg);
            Stage3_Early(cfg);
            Stage3_Late(cfg);
            Stage3_WrongLevel(cfg);
            Stage3_NoRelease(cfg);
            Stage3_ShortHold(cfg);
            Stage3_PreSqueezeForgiven(cfg);

            string summary = "[SelfTest] 단계 규칙: " + passed + " 통과, " + failed + " 실패";
            if (failed == 0)
                Debug.Log(summary);
            else
                Debug.LogError(summary);
        }

        // ---------- 1단계 ----------

        private static void Stage1_GripRelease_TenKills(GameConfig original)
        {
            // 규칙 확인이 목적이라 제한 시간에 걸리지 않게 넉넉히 (실제 값은 GameConfig 그대로)
            GameConfig cfg = UnityEngine.Object.Instantiate(original);
            cfg.s1TimeLimitSeconds = cfg.s1TargetKills * (0.4f + RespawnWait(cfg)) + 5f;
            var stage = Begin(new Stage1Monster(), cfg);
            double t = 0d;

            for (int i = 0; i < cfg.s1TargetKills; i++)
            {
                t = Feed(stage, t, 100f, 0.4f);
                t = Feed(stage, t, 0f, RespawnWait(cfg));
            }

            StageResult r = stage.End();
            Check("1단계: 쥐었다 놓기 10번 → 10마리, 완료", stage.IsDone && stage.Kills == cfg.s1TargetKills);
            Check("1단계: 유효 반복 10회, 성공 10회", r.repetitions == cfg.s1TargetKills && r.successes == cfg.s1TargetKills);
            Check("1단계: 반응시간 기록됨", r.reactionMsMean > 0f);
            Check("1단계: 완료 문구 s1.clear", stage.MessageKey == Stage1Monster.MsgClear);
            UnityEngine.Object.DestroyImmediate(cfg);
        }

        private static void Stage1_HoldWithoutRelease_NoNextMonster(GameConfig cfg)
        {
            var stage = Begin(new Stage1Monster(), cfg);
            double t = Feed(stage, 0d, 100f, cfg.s1HoldTooLongSeconds + 3f);

            Check("1단계: 계속 쥐고 있으면 다음 몬스터 안 나옴 (시행 1회)", stage.Kills == 1 && stage.Tries == 1);
            Check("1단계: 쥔 채 2초 → s1.holdTooLong", stage.MessageKey == Stage1Monster.MsgHoldTooLong);

            // 20~50 사이(애매한 힘)로 내려도 놓음이 아님
            t = Feed(stage, t, 35f, 2f);
            Check("1단계: 35% 는 놓음 아님 (여전히 시행 1회)", stage.Tries == 1);

            float shortWait = cfg.s1RespawnDelayMin * 0.5f;
            t = Feed(stage, t, 0f, shortWait);
            Check("1단계: 힘을 뺀 직후(몬스터 나오기 전)는 s1.wait", stage.MessageKey == Stage1Monster.MsgWait && !stage.MonsterOut);

            t = Feed(stage, t, 0f, RespawnWait(cfg) - shortWait);
            Check("1단계: 힘을 빼면 다음 몬스터 등장", stage.Tries == 2 && stage.MonsterOut);
            Check("1단계: 몬스터가 나오는 순간 s1.again", stage.MessageKey == Stage1Monster.MsgAgain);
        }

        private static void Stage1_SqueezeDuringRest_Delays(GameConfig cfg)
        {
            var stage = Begin(new Stage1Monster(), cfg);
            double t = Feed(stage, 0d, 100f, 0.3f);
            t = Feed(stage, t, 0f, 0.3f);
            t = Feed(stage, t, 35f, 2f); // 쉬는 중에 애매하게(20% 초과) 힘을 줌

            Check("1단계: 쉬는 중 힘을 주면 몬스터가 안 나옴", !stage.MonsterOut && stage.Tries == 1);
            Check("1단계: 쉬는 중 힘을 주면 s1.rest", stage.MessageKey == Stage1Monster.MsgRestAgain);

            t = Feed(stage, t, 0f, RespawnWait(cfg));
            Check("1단계: 다시 힘을 빼고 기다리면 몬스터 등장", stage.MonsterOut && stage.Tries == 2);
        }

        private static void Stage1_TelegraphCancel(GameConfig cfg)
        {
            var stage = Begin(new Stage1Monster(), cfg);
            double t = Feed(stage, 0d, 100f, 0.3f);

            // 예고가 시작될 때까지 힘을 뺀 채로 진행
            int guard = Mathf.CeilToInt(RespawnWait(cfg) / Dt);
            while (!stage.IsTelegraphing && guard-- > 0)
            {
                stage.Tick(0f, t);
                t += Dt;
            }

            Check("1단계: 쉬기를 마치면 등장 예고 시작", stage.IsTelegraphing && !stage.MonsterOut);

            stage.Tick(35f, t);
            t += Dt;
            Check("1단계: 예고 중 힘을 주면 예고 취소 → 다시 쉬기", !stage.IsTelegraphing && !stage.MonsterOut &&
                                                          stage.MessageKey == Stage1Monster.MsgRestAgain);
        }

        private static void Stage1_MaxWaitFallback(GameConfig cfg)
        {
            if (cfg.s1RestMaxWaitSeconds <= 0f)
                return;

            var stage = Begin(new Stage1Monster(), cfg);
            double t = Feed(stage, 0d, 100f, 0.3f);
            t = Feed(stage, t, 0f, Dt * 2f);
            t = Feed(stage, t, 60f, cfg.s1RestMaxWaitSeconds + cfg.s1TelegraphSeconds + 0.2f); // 계속 쥐고 있음

            Check("1단계: 힘을 못 빼도 최대 대기 후 등장 (안전장치)", stage.MonsterOut && stage.Tries == 2);
            Check("1단계: 쥔 채 등장 → s1.releaseFirst, 처치 안 됨",
                stage.BlockedByPreGrip && stage.MessageKey == Stage1Monster.MsgReleaseFirst && stage.Kills == 1);

            t = Feed(stage, t, 0f, 0.1f);
            Check("1단계: 힘을 빼면 s1.again", stage.MessageKey == Stage1Monster.MsgAgain);
            t = Feed(stage, t, 100f, 0.1f);
            Check("1단계: 다시 쥐면 처치", stage.Kills == 2);
        }

        /// <summary>힘을 뺀 뒤 다음 몬스터가 확실히 나올 때까지의 시간 (최대 쉬기 + 예고 + 여유).</summary>
        private static float RespawnWait(GameConfig cfg)
        {
            return cfg.s1RespawnDelayMax + cfg.s1TelegraphSeconds + 0.2f;
        }

        private static void Stage1_NoGrip_TimeUp(GameConfig cfg)
        {
            var stage = Begin(new Stage1Monster(), cfg);
            Feed(stage, 0d, 0f, cfg.s1TimeLimitSeconds + 0.5f);

            Check("1단계: 안 쥐면 제한 시간 뒤 완료", stage.IsDone && stage.Kills == 0);
            Check("1단계: 시간 종료 문구 s1.partial", stage.MessageKey == Stage1Monster.MsgPartial);
        }

        // ---------- 2단계 ----------

        private static void Stage2_HoldInBand_Launches(GameConfig cfg)
        {
            var stage = Begin(new Stage2Charge(), cfg);
            double t = 0d;

            for (int i = 0; i < cfg.s2LaunchTarget; i++)
            {
                t = Feed(stage, t, 50f, cfg.s2HoldSeconds + 0.1f);
                t = Feed(stage, t, 50f, cfg.s2LaunchSequenceSeconds + 0.1f);
                t = Feed(stage, t, 0f, 0.3f);
            }

            Check("2단계: 50% 3초 유지 ×3 → 발사 3회, 완료", stage.IsDone && stage.Launches == cfg.s2LaunchTarget);
            Check("2단계: 완료 문구 s2.clear", stage.MessageKey == Stage2Charge.MsgClear);
        }

        private static void Stage2_NeedsReleaseBetweenTrials(GameConfig cfg)
        {
            var stage = Begin(new Stage2Charge(), cfg);
            double t = Feed(stage, 0d, 50f, cfg.s2HoldSeconds + 0.1f);
            t = Feed(stage, t, 50f, cfg.s2LaunchSequenceSeconds + 5f);

            Check("2단계: 발사 후 힘을 안 빼면 다음 발사 없음", stage.Launches == 1 && stage.Tries == 1);
            Check("2단계: 힘 빼기 안내 s2.relax", stage.MessageKey == Stage2Charge.MsgRelax);

            t = Feed(stage, t, 0f, 0.2f);
            Check("2단계: 힘을 빼면 다음 시행 시작", stage.Tries == 2);
        }

        private static void Stage2_ExitGrace(GameConfig cfg)
        {
            float outside = cfg.s2BandCenter + cfg.s2BandWidth; // 구간 위
            float shortOut = cfg.s2ExitGraceSeconds * 0.5f;
            float longOut = cfg.s2ExitGraceSeconds * 2f;

            var stage = Begin(new Stage2Charge(), cfg);
            double t = Feed(stage, 0d, 50f, 2f);
            t = Feed(stage, t, outside, shortOut);
            t = Feed(stage, t, 50f, cfg.s2HoldSeconds - 2f + 0.15f);
            Check("2단계: 유예 안쪽 짧은 이탈은 유지 시간 유지 → 발사", stage.Launches == 1);

            stage = Begin(new Stage2Charge(), cfg);
            t = Feed(stage, 0d, 50f, 2f);
            t = Feed(stage, t, outside, longOut);
            t = Feed(stage, t, 50f, cfg.s2HoldSeconds - 2f + 0.15f);
            Check("2단계: 유예를 넘긴 이탈은 유지 시간 초기화 → 발사 안 됨", stage.Launches == 0);
        }

        private static void Stage2_Metrics(GameConfig cfg)
        {
            var stage = Begin(new Stage2Charge(), cfg);
            Feed(stage, 0d, cfg.s2BandCenter + 4f, cfg.s2HoldSeconds + 0.1f);
            StageResult r = stage.End();

            Check("2단계: 정확도(중심과 차이 평균) ≈ 4", Mathf.Abs(r.accuracyMae - 4f) < 0.01f);
            Check("2단계: 같은 힘 유지 → 표준편차 ≈ 0", r.holdSd >= 0f && r.holdSd < 0.01f);
            Check("2단계: 구간 폭 기록", Mathf.Approximately(r.bandWidth, cfg.s2BandWidth));
        }

        // ---------- 3단계 ----------

        /// <summary>노트 k 의 등급 가운데 힘.</summary>
        private static float Center(Stage3Rhythm s, int k)
        {
            int grade = s.GradeOf(k);
            return (s.GradeMin(grade) + s.GradeMax(grade)) * 0.5f;
        }

        /// <summary>
        /// 3단계를 처음부터 끝까지(또는 maxSeconds 까지) 60 fps 로 돌린다.
        /// input(stage, t) 가 그 시각의 힘을 돌려준다.
        /// </summary>
        private static Stage3Rhythm RunStage3(GameConfig cfg, Func<Stage3Rhythm, double, float> input,
            double maxSeconds = 120d)
        {
            var stage = Begin(new Stage3Rhythm(), cfg);
            for (double t = 0d; t < maxSeconds && !stage.IsDone; t += Dt)
                stage.Tick(input(stage, t), t);
            return stage;
        }

        /// <summary>t 가 어떤 노트의 [시작 + from, 끝 + to) 안이면 그 노트 번호, 아니면 -1.</summary>
        private static int NoteAt(Stage3Rhythm s, double t, double fromOffset, double toOffset)
        {
            for (int k = 0; k < s.NoteCount; k++)
            {
                if (t >= s.NowOf(k) + fromOffset && t < s.HoldEndOf(k) + toOffset)
                    return k;
            }

            return -1;
        }

        private static void Stage3_PerfectPlay(GameConfig cfg)
        {
            Stage3Rhythm s = RunStage3(cfg, (st, t) =>
            {
                int k = NoteAt(st, t, 0d, 0d);
                return k >= 0 ? Center(st, k) : 0f;
            });
            StageResult r = s.End();

            Check("3단계: '지금'에 맞춰 등급대로 쥐고 버티면 전부 성공", s.IsDone && s.Successes == s.NoteCount);
            Check("3단계: 정확도(등급 중심과 차이) ≈ 0", r.accuracyMae >= 0f && r.accuracyMae < 0.5f);
            Check("3단계: 쥐기 타이밍 오차 기록 (지연 보정만큼)", r.timingErrMs >= 0f && r.timingErrMs <= cfg.s3InputLatencyMs + 40f);
        }

        private static void Stage3_Early(GameConfig cfg)
        {
            Stage3Rhythm s = RunStage3(cfg, (st, t) =>
            {
                int k = NoteAt(st, t, -0.5d, 0d);
                return k >= 0 ? Center(st, k) : 0f;
            });

            Check("3단계: '지금'보다 0.5초 일찍 쥐면 성공 없음, 마지막 결과 '빨랐어'",
                s.Successes == 0 && s.LastResult == NoteResult.Early);
        }

        private static void Stage3_Late(GameConfig cfg)
        {
            Stage3Rhythm s = RunStage3(cfg, (st, t) =>
            {
                int k = NoteAt(st, t, 0.5d, 0.5d);
                return k >= 0 ? Center(st, k) : 0f;
            });

            Check("3단계: '지금'보다 0.5초 늦게 쥐면 성공 없음, 마지막 결과 '늦었어'",
                s.Successes == 0 && s.LastResult == NoteResult.Late);
        }

        private static void Stage3_WrongLevel(GameConfig cfg)
        {
            Stage3Rhythm s = RunStage3(cfg, (st, t) =>
            {
                int k = NoteAt(st, t, 0d, 0d);
                return k >= 0 ? Mathf.Min(100f, Center(st, k) + 30f) : 0f;
            });

            Check("3단계: 타이밍은 맞지만 등급보다 30%p 세게 버팀 → 성공 없음, '힘이 달라요'",
                s.Successes == 0 && s.LastResult == NoteResult.WrongLevel);
        }

        private static void Stage3_NoRelease(GameConfig cfg)
        {
            // 빼기 타이밍은 판정하지 않는다. 다만 다음 노트까지 계속 쥐고 있으면 다음 노트는 '빨랐어'.
            var stage = Begin(new Stage3Rhythm(), cfg);
            double end = stage.HoldEndOf(1) + 0.5d;
            for (double t = 0d; t < end; t += Dt)
                stage.Tick(t >= stage.NowOf(0) ? Center(stage, 0) : 0f, t);

            Check("3단계: 버틴 뒤 안 빼도 그 노트는 성공 (빼기 타이밍은 판정 안 함)", stage.Successes >= 1);
            Check("3단계: 다음 노트까지 계속 쥐고 있으면 다음 노트는 '빨랐어'",
                stage.JudgedCount == 2 && stage.LastResult == NoteResult.Early);
        }

        private static void Stage3_ShortHold(GameConfig cfg)
        {
            // 쥐기 시작은 맞지만 버티기 필요 시간의 절반만 구간 안에 있으면 '힘이 달라요'
            var stage = Begin(new Stage3Rhythm(), cfg);
            double holdSeconds = stage.HoldEndOf(0) - stage.NowOf(0);
            double inBand = holdSeconds * cfg.s3HoldRequiredFraction * 0.5d;
            double end = stage.HoldEndOf(0) + 0.5d;
            for (double t = 0d; t < end; t += Dt)
            {
                float g = 0f;
                if (t >= stage.NowOf(0) && t < stage.NowOf(0) + inBand)
                    g = Center(stage, 0);
                else if (t >= stage.NowOf(0) + inBand && t < stage.HoldEndOf(0))
                    g = Mathf.Min(100f, Center(stage, 0) + 30f);
                stage.Tick(g, t);
            }

            Check("3단계: 구간 안에서 충분히 못 버티면 '힘이 달라요'",
                stage.JudgedCount == 1 && stage.LastResult == NoteResult.WrongLevel);
        }

        private static void Stage3_PreSqueezeForgiven(GameConfig cfg)
        {
            var stage = Begin(new Stage3Rhythm(), cfg);
            double early = stage.NoteStartOf(0) + 0.2d;
            double end = stage.HoldEndOf(0) + 1d;

            for (double t = 0d; t < end; t += Dt)
            {
                float g = 0f;
                if (t >= early && t < early + 0.2d)
                    g = Center(stage, 0); // 카운트다운 중에 살짝 쥐었다가 뺌
                else if (t >= stage.NowOf(0) && t < stage.HoldEndOf(0))
                    g = Center(stage, 0);
                stage.Tick(g, t);
            }

            Check("3단계: 카운트다운 중 쥐었다 뺀 건 봐주고, '지금'에 다시 쥐면 성공",
                stage.JudgedCount == 1 && stage.LastResult == NoteResult.Good);
        }

        // ---------- 도우미 ----------

        private static T Begin<T>(T stage, GameConfig cfg) where T : StageBase
        {
            stage.Begin(StageConfig.FromDefaults(cfg));
            return stage;
        }

        /// <summary>g 를 seconds 동안 60 fps 로 넣는다. 다음 시각을 돌려준다.</summary>
        private static double Feed(StageBase stage, double t, float g, float seconds)
        {
            int frames = Mathf.CeilToInt(seconds / Dt);
            for (int i = 0; i < frames; i++)
            {
                stage.Tick(g, t);
                t += Dt;
            }

            return t;
        }

        private static void Check(string name, bool ok)
        {
            if (ok)
            {
                passed++;
                Debug.Log("[SelfTest] PASS  " + name);
            }
            else
            {
                failed++;
                Debug.LogError("[SelfTest] FAIL  " + name);
            }
        }
    }
}
