using Malang.Core;
using Malang.Training;
using UnityEngine;
using UnityEngine.UI;

namespace Malang.UI
{
    /// <summary>
    /// 메인 화면: 레벨·경험치, 자물쇠(보호자 모드), 캐릭터와 말풍선, 시작 버튼.
    /// M1: 레벨·경험치는 GameConfig 의 시연용 값으로 표시한다. M4 에서 ProgressManager 로 바뀐다.
    /// </summary>
    public class MainScreen : ScreenBase
    {
        [SerializeField] private Button startButton;
        [SerializeField] private LongPressButton lockButton;
        [SerializeField] private GuardianPanel guardian;
        [SerializeField] private LocText levelNumber;
        [SerializeField] private RectTransform xpFill;
        [SerializeField] private LocText toNextText;

        public override ScreenId Id => ScreenId.Main;

        private void Awake()
        {
            if (startButton != null)
                startButton.onClick.AddListener(OnStart);
            if (lockButton != null)
                lockButton.LongPressed += OnLockHeld;
        }

        private void OnDestroy()
        {
            if (startButton != null)
                startButton.onClick.RemoveListener(OnStart);
            if (lockButton != null)
                lockButton.LongPressed -= OnLockHeld;
        }

        public override void OnEnter()
        {
            ShowProgress(Config.demoLevel, Config.demoXp);
        }

        public override void OnExit()
        {
            if (guardian != null)
                guardian.Close();
        }

        private void ShowProgress(int level, int xp)
        {
            int need = Config.XpNeededAt(level);

            if (levelNumber != null)
                levelNumber.SetRaw(level.ToString());

            if (xpFill != null)
            {
                Vector2 max = xpFill.anchorMax;
                max.x = need > 0 ? Mathf.Clamp01((float)xp / need) : 0f;
                xpFill.anchorMax = max;
            }

            if (toNextText != null)
                toNextText.SetArgs(Mathf.Max(0, need - xp));
        }

        private void OnStart()
        {
            SessionManager.Begin();
            Go(ScreenId.Stage1);
        }

        private void OnLockHeld()
        {
            if (guardian != null)
                guardian.Open();
        }
    }
}
