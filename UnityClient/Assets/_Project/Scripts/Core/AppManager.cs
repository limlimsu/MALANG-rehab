using Malang.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Malang.Core
{
    /// <summary>
    /// 앱 시작 시 설정·언어를 준비하고 전역 단축키를 처리한다.
    /// 단축키: F1 디버그 오버레이, F2 현재 단계 즉시 완료, F4 언어 전환 (F3 은 M4 에서 추가)
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class AppManager : MonoBehaviour
    {
        /// <summary>
        /// 현재 코드가 기대하는 씬 구성 버전. 씬 구성이 바뀌면 올린다 (SceneBuilder 가 씬에 기록).
        /// 1 = M1, 2 = M2, 3 = M3(1·2단계), 4 = 몬스터 쓰러짐 연출, 5 = 게이지 구간·목표 말풍선,
        /// 6 = 1단계 덤불·등장 예고, 7 = 단계 완료 카드, 8 = 2단계 큰 로켓·불꽃·연기, 행성 제거,
        /// 9 = 2단계 발사대·새 로켓 올라오기, 10 = 발사대를 언덕 해치로, 11 = 발사대를 구름으로,
        /// 12 = 넓고 낮은 구름 띠·로켓 띄우기·로켓 코, 13 = 구름 제거·그림자·위에서 착지·삼각형 로켓 코,
        /// 14 = 그림자 제거
        /// </summary>
        public const int ExpectedSceneVersion = 16; // 15 = 3단계 신호→반응·음악·소리 설정, 16 = 3단계 카운트다운 링·버티기 게이지

        [SerializeField] private ScreenRouter router;
        [SerializeField] private DebugOverlay debugOverlay;
        [Tooltip("SceneBuilder 가 기록하는 씬 구성 버전. 코드와 다르면 Play 전에 다시 빌드하라고 알린다.")]
        [SerializeField] private int sceneVersion;

        public static AppManager Instance { get; private set; }

        public ScreenRouter Router => router;
        public int SceneVersion => sceneVersion;
        public GameConfig Config { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("AppManager: 이미 있어 중복 인스턴스를 제거합니다.");
                Destroy(gameObject);
                return;
            }

            Instance = this;

            if (sceneVersion != ExpectedSceneVersion)
                Debug.LogError("MALANG: 씬이 예전 구성(v" + sceneVersion + ")입니다. 코드는 v" + ExpectedSceneVersion +
                               " 를 기대합니다. Play 를 멈추고 메뉴 MALANG > Build Scene 을 실행하세요.");

            Config = GameConfig.Get();
            Application.targetFrameRate = Config.targetFrameRate;

            // M4 에서 profile.json 의 language 로 바뀐다.
            Loc.SetLanguage(Config.defaultLanguage);
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        private void Update()
        {
            // Space 는 모의 입력의 "쥐기" 키다. 마지막으로 클릭한 버튼이 선택된 채로 남아
            // Space(Submit)에 다시 눌리지 않도록 UI 선택 상태를 항상 비운다. (터치 앱이라 키보드 탐색은 쓰지 않음)
            EventSystem eventSystem = EventSystem.current;
            if (eventSystem != null && eventSystem.currentSelectedGameObject != null)
                eventSystem.SetSelectedGameObject(null);

            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
                return;

            if (keyboard.f1Key.wasPressedThisFrame && debugOverlay != null)
                debugOverlay.Toggle();

            if (keyboard.f2Key.wasPressedThisFrame && router != null)
            {
                var stageScreen = router.Active as StageScreenBase;
                if (stageScreen != null)
                    stageScreen.DebugComplete();
            }

            if (keyboard.f4Key.wasPressedThisFrame)
                Loc.Toggle();
        }
    }
}
