using TMPro;
using UnityEngine;

namespace Malang.Core
{
    /// <summary>보정 시 최대 측정 3회 중 대푯값을 고르는 방식.</summary>
    public enum MaxAggregate
    {
        Max,
        Median
    }

    /// <summary>
    /// 모든 조정 가능한 수치를 모아 둔 설정. 기본값은 개발 명세서(SPEC) 값이다.
    /// 에셋 위치: Resources/Config/GameConfig.asset (MALANG > Build Scene 이 없으면 만든다)
    /// </summary>
    [CreateAssetMenu(menuName = "MALANG/Game Config", fileName = "GameConfig")]
    public class GameConfig : ScriptableObject
    {
        public const string ResourcePath = "Config/GameConfig";

        private static GameConfig cached;

        public static GameConfig Get()
        {
            if (cached == null)
            {
                cached = Resources.Load<GameConfig>(ResourcePath);

                if (cached == null)
                {
                    Debug.LogError("GameConfig: Resources/" + ResourcePath +
                                   " 이 없습니다. MALANG > Build Scene 을 실행하세요. 기본값으로 진행합니다.");
                    cached = CreateInstance<GameConfig>();
                }
            }

            return cached;
        }

        /// <summary>캐시를 비운다(에디터 빌더가 에셋을 새로 만든 뒤 호출).</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void ClearCache()
        {
            cached = null;
        }

        [Header("App · UI")]
        public string defaultLanguage = "ko";
        public int targetFrameRate = 60;
        [Tooltip("모의 입력일 때 부트 화면에서 기다리는 시간(초)")]
        public float bootDelaySeconds = 0.8f;
        [Tooltip("자물쇠를 이 시간(초) 동안 누르고 있어야 보호자 모드가 열린다")]
        public float guardianHoldSeconds = 2f;
        public float characterSquishX = 0.0016f;
        public float characterSquishY = 0.0022f;
        [Tooltip("단계가 끝나고 완료 카드가 뜰 때까지 기다리는 시간(초) — 마지막 연출이 끝나도록")]
        public float stageCompleteDelaySeconds = 0.8f;
        public float stageCompletePopSeconds = 0.3f;

        [Header("Fonts (Build Scene 이 자동 연결)")]
        public TMP_FontAsset koTitleFont;
        public TMP_FontAsset koBodyFont;
        public TMP_FontAsset enTitleFont;
        public TMP_FontAsset enBodyFont;

        [Header("Input · Mock")]
        public float mockRiseRate = 2.2f;
        public float mockFallRate = 5.6f;
        public float mockZeroSnap = 0.3f;
        public bool mockBypassCalibration = true;
        [Tooltip("↑/↓ 키로 목표 세기를 바꾸는 단위 (%p)")]
        public float mockFineStep = 5f;

        [Header("Input · Sensor")]
        [Range(0f, 1f)] public float emaAlpha = 0.3f;
        public int udpPort = 5005;
        [Tooltip("UDP 수신 스레드가 종료 확인을 위해 깨어나는 간격 (ms)")]
        public int udpReceiveTimeoutMs = 500;
        public float disconnectTimeoutSeconds = 3f;
        public int serialBaudRate = 115200;
        public int rawChannelMax = 4095;

        [Header("Calibration")]
        public float calRestSeconds = 2f;
        [Tooltip("휴식값 측정 중 채널 합 표준편차가 이 값을 넘으면 다시 측정 (원시값 단위)")]
        public float calRestStableSD = 40f;
        public int calMaxTrials = 3;
        public float calMaxWindowSeconds = 3f;
        [Tooltip("최대 측정 피크 간 편차 비율이 이 값을 넘으면 1회 추가")]
        public float calMaxPeakDeviation = 0.2f;
        public int calMaxExtraTrials = 1;
        [Tooltip("max - rest 가 이 값보다 작으면 보정 실패 (채널 합 원시값 단위)")]
        public float calMinRange = 200f;
        public int calRemeasureDays = 7;
        public float calNearMaxPercent = 95f;
        public float calNearMaxRatio = 0.3f;
        public MaxAggregate calMaxAggregate = MaxAggregate.Max;

        [Header("Grip detection (1·2단계)")]
        public float gripThreshold = 50f;
        public float releaseThreshold = 20f;

        [Header("Stage 1 · Monster")]
        public int s1TargetKills = 10;
        public float s1TimeLimitSeconds = 60f;
        [Tooltip("다음 몬스터 전에 힘을 뺀 상태(≤ releaseThreshold)를 계속 유지해야 하는 시간 — 이 범위에서 무작위")]
        public float s1RespawnDelayMin = 0.6f;
        public float s1RespawnDelayMax = 1.4f;
        [Tooltip("등장 예고(덤불 흔들림 + !) 시간")]
        public float s1TelegraphSeconds = 1.2f;
        [Tooltip("예고 중 \"!\" 가 나타나는 시점 (예고 시간 대비 비율). 그 전에는 덤불만 점점 세게 흔들린다")]
        [Range(0f, 1f)] public float s1AlertAtFraction = 0.5f;
        [Tooltip("예고 시작 때 덤불 흔들림 세기 (끝으로 갈수록 1까지)")]
        [Range(0f, 1f)] public float s1BushShakeStart = 0.3f;
        [Tooltip("힘을 계속 못 빼도 이 시간이 지나면 강제로 등장 (0 = 끔)")]
        public float s1RestMaxWaitSeconds = 4f;
        public float s1BushShakeDegrees = 6f;
        public float s1BushShakeHz = 8f;
        public float s1HoldTooLongSeconds = 2f;
        public float s1SuccessGripWindowSeconds = 3f;
        public float s1HitAnimSeconds = 0.3f;
        public float s1SpawnAnimSeconds = 0.2f;
        [Tooltip("피격 후 × 눈으로 멈춰 있는 시간(초)")]
        public float s1DefeatHoldSeconds = 0.3f;
        [Tooltip("그 뒤 작아지며 흐려져 사라지는 시간(초)")]
        public float s1DefeatFadeSeconds = 0.4f;
        [Range(0f, 1f)] public float s1DefeatEndScale = 0.6f;
        [Tooltip("처치 후 게이지에 쉬기 구간 말풍선(쭉~)을 보여 줄지")]
        public bool s1ShowRelaxCallout = true;

        [Header("Stage 2 · Charge")]
        public float s2BandCenter = 50f;
        public float s2BandWidth = 20f;
        public float s2HoldSeconds = 3f;
        public float s2ExitGraceSeconds = 0.3f;
        public int s2LaunchTarget = 3;
        public float s2TrialTimeLimitSeconds = 20f;
        public float s2LaunchSequenceSeconds = 1.6f;
        public float s2RocketFlySeconds = 0.9f;
        [Tooltip("발사 전 점화(부르르 + 불꽃 커짐 + 바닥 연기) 시간")]
        public float s2IgnitionSeconds = 0.3f;
        [Tooltip("힘을 뺀 뒤 새 로켓이 위에서 내려와 착지하기까지 시간")]
        public float s2RocketLandSeconds = 0.8f;
        [Tooltip("착지 후 살짝 통 튀는 시간")]
        public float s2LandBounceSeconds = 0.2f;
        [Tooltip("로켓이 날아오르는 거리 (기준 해상도 px, 시안 60cqw)")]
        public float s2RocketFlyHeight = 1152f;
        [Range(0f, 1f)] public float s2FlameOffAlpha = 0.3f;

        [Header("Stage 3 · Rhythm (신호→반응 방식)")]
        [Tooltip("노래·판정 박자 (BPM)")]
        public float s3Bpm = 110f;
        public int s3BeatsPerBar = 4;
        [Tooltip("첫 노트 전에 노래만 들려주는 마디 수")]
        public int s3LeadInBars = 1;
        [Tooltip("노트 하나에 쓰는 마디 수 (1마디째 카운트다운, 2마디째 지금·유지·빼기)")]
        public int s3BarsPerNote = 2;
        [Tooltip("\"지금\" 부터 버티기 시간 (박 수)")]
        public float s3HoldBeats = 2f;
        [Tooltip("쥐기 시작으로 보는 힘 (%) — 놓음 임계(s3ReleaseThreshold)보다 커야 함")]
        public float s3OnsetThreshold = 12f;
        [Tooltip("센서·필터 지연 보정 (ms). 측정 시각을 이만큼 앞당겨 판정")]
        public float s3InputLatencyMs = 80f;
        [Tooltip("버티기: 유지 시간 중 이 비율 이상을 등급 구간 안에서 보내면 성공 (버티기 게이지가 꽉 참)")]
        [Range(0.1f, 1f)] public float s3HoldRequiredFraction = 0.6f;
        [Range(0f, 1f)] public float s3MusicVolume = 0.55f;
        [Range(0f, 1f)] public float s3SfxVolume = 0.9f;
        public float s3NotePopSeconds = 0.2f;
        public int s3NoteCount = 12;
        [Tooltip("레인 방식용 (현재 신호→반응 방식에서는 미사용)")]
        public float s3NoteIntervalSeconds = 1.7f;
        public float s3NoteHoldSeconds = 0.8f;
        public float s3TravelSeconds = 2.5f;
        public float s3SoftMin = 20f;
        public float s3SoftMax = 40f;
        public float s3NormalMin = 40f;
        public float s3NormalMax = 60f;
        public float s3StrongMin = 60f;
        public float s3StrongMax = 80f;
        [Tooltip("등급 정확 판정 허용 오차 (%p)")]
        public float s3GradeTolerance = 5f;
        public float s3WindowMs = 200f;
        [Tooltip("3단계 놓음 판정 임계값 (1·2단계는 releaseThreshold 사용)")]
        public float s3ReleaseThreshold = 10f;
        public float s3ReleaseExtraMs = 100f;
        [Tooltip("고정 노트 패턴. 0 = 살짝, 1 = 보통, 2 = 세게. 노트 수보다 짧으면 반복")]
        public int[] s3Pattern = { 0, 1, 2, 1, 0, 2, 1, 2, 0, 2, 1, 0 };
        public float s3NoteSuccessAnimSeconds = 0.25f;

        [Header("Adaptive difficulty")]
        public int diffRecentTrials = 5;
        public float diffRaiseAt = 0.8f;
        public float diffLowerAt = 0.5f;
        public float diffS2WidthStep = 5f;
        public float diffS2WidthMin = 10f;
        public float diffS2WidthMax = 40f;
        public float diffS3WindowStepMs = 25f;
        public float diffS3WindowMinMs = 100f;
        public float diffS3WindowMaxMs = 400f;

        [Header("Progress")]
        public int xpPerLevelLow = 150;
        public int xpPerLevelHigh = 250;
        [Tooltip("이 레벨부터 xpPerLevelHigh 적용")]
        public int xpHighFromLevel = 5;
        public int sproutLevel = 5;
        public int xpStageComplete = 30;
        public int xpDailyCourse = 50;
        public int xpNewRecord = 20;
        public int weeklyGoal = 5;
        public float xpRevealIntervalSeconds = 0.55f;

        [Header("Demo profile (첫 실행 · 시연용 초기화)")]
        public string demoChildCode = "DEMO-01";
        public int demoLevel = 4;
        public int demoXp = 90;

        [Header("Session log")]
        public int traceHz = 20;

        [Header("Debug")]
        [Tooltip("디버그 오버레이 글자 갱신 주기 (Hz)")]
        public float debugRefreshHz = 10f;

        /// <summary>현재 레벨에서 다음 레벨까지 필요한 총 경험치.</summary>
        public int XpNeededAt(int level)
        {
            return level >= xpHighFromLevel ? xpPerLevelHigh : xpPerLevelLow;
        }
    }
}
