using System;
using Malang.Core;

namespace Malang.Training
{
    /// <summary>훈련 단계 계약 (SPEC §4). 게임 규칙은 Tick 으로 받은 g 만 본다.</summary>
    public interface IStage
    {
        void Begin(StageConfig cfg);

        /// <summary>매 프레임. t = 세션 시작 후 초 (연결 끊김 일시정지 동안은 멈춘 시계).</summary>
        void Tick(float grip, double t);

        bool IsDone { get; }
        StageResult End();
    }

    /// <summary>
    /// 단계 시작 시 넘기는 설정. 기본값은 GameConfig, 적응형 난이도(M4)가 일부 값을 덮어쓴다.
    /// </summary>
    public class StageConfig
    {
        public GameConfig Game;
        public float S2BandWidth;
        public float S3WindowMs;

        public static StageConfig FromDefaults(GameConfig game)
        {
            return new StageConfig
            {
                Game = game,
                S2BandWidth = game.s2BandWidth,
                S3WindowMs = game.s3WindowMs
            };
        }
    }

    /// <summary>
    /// 단계 결과 + 지표 (SPEC §11, §12 sessions JSON 의 stages 항목).
    /// 해당 없는 지표는 -1.
    /// </summary>
    [Serializable]
    public class StageResult
    {
        public const float None = -1f;

        public int stage;
        public int tries;
        public int successes;
        /// <summary>아동 화면에 보이는 점수 (몬스터 수, 발사 수, 리듬 성공 수).</summary>
        public int score;

        // 1단계
        public float reactionMsMean = None;
        public int repetitions;

        // 2·3단계
        public float bandWidth = None;
        public float accuracyMae = None;
        public float holdSd = None;

        // 3단계
        public float windowMs = None;
        public float timingErrMs = None;
    }
}
