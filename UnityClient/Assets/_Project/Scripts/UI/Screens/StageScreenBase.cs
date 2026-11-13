using Malang.Core;
using Malang.Input;
using Malang.Training;
using UnityEngine;
using UnityEngine.UI;

namespace Malang.UI
{
    /// <summary>
    /// 1–3단계 화면 공통: 상단 바(단계 칩·진행 점·타이머), 플레이 영역, 오른쪽 힘 게이지, "다음" 버튼.
    /// - 단계 규칙(StageBase)을 만들어 매 프레임 IGripInput.Grip 으로 Tick 한다.
    /// - 센서 연결이 끊기면 Tick 과 세션 시계를 멈춘다(일시정지).
    /// - 규칙이 끝나면(IsDone) 잠시 뒤 가운데 완료 카드(제목 · 아이콘 × 개수 · "다음 재활")를 띄운다.
    ///   완료 카드가 있는 단계는 구석의 "다음" 버튼을 숨긴다.
    /// - 규칙이 없는 단계(3단계, 아직 미구현)는 구석 "다음" 버튼이 처음부터 켜져 있다.
    /// </summary>
    public abstract class StageScreenBase : ScreenBase
    {
        [SerializeField] protected Button nextButton;
        [SerializeField] protected Image nextBackground;
        [SerializeField] protected LocText nextLabel;
        [SerializeField] protected Shadow nextShadow;
        [SerializeField] protected LocText counterText;
        [SerializeField] protected LocText messageText;
        [SerializeField] protected ForceGauge gauge;
        [SerializeField] protected StageCompletePanel completePanel;

        private StageBase stage;
        private bool nextReady;
        private bool paused;
        private int shownMessageVersion = -1;

        protected StageBase Stage => stage;

        /// <summary>"다음" 을 눌렀을 때 갈 화면.</summary>
        protected abstract ScreenId NextScreen { get; }

        protected virtual void Awake()
        {
            if (nextButton != null)
                nextButton.onClick.AddListener(OnNext);
            if (completePanel != null)
                completePanel.NextClicked += OnNext;
        }

        protected virtual void OnDestroy()
        {
            if (nextButton != null)
                nextButton.onClick.RemoveListener(OnNext);
            if (completePanel != null)
                completePanel.NextClicked -= OnNext;
        }

        public override void OnEnter()
        {
            stage = CreateStage();
            if (stage != null)
            {
                stage.Begin(StageConfig.FromDefaults(Config));
                BindStage(stage);
            }

            bool useCard = stage != null && completePanel != null;
            if (completePanel != null)
                completePanel.Hide();
            if (nextButton != null)
                nextButton.gameObject.SetActive(!useCard);

            SetNextReady(stage == null);
            shownMessageVersion = -1;
            paused = false;
            ResetView();
            if (gauge != null)
                ConfigureGauge(gauge);
            RefreshMessage();
        }

        public override void OnExit()
        {
            if (stage != null)
                UnbindStage(stage);
            StopAllCoroutines();
            if (completePanel != null)
                completePanel.Hide();
        }

        private void Update()
        {
            if (stage == null)
                return;

            // 일시정지: 센서 연결이 끊긴 동안은 규칙과 시계를 멈춘다.
            IGripInput input = GripSource.Input;
            bool nowPaused = input == null || !input.IsConnected;
            if (nowPaused != paused)
            {
                paused = nowPaused;
                OnPauseChanged(paused);
            }

            if (paused)
                return;

            SessionManager.Advance(Time.deltaTime);
            stage.Tick(input.Grip, StageTime());

            UpdateView(stage);
            RefreshMessage();

            if (stage.IsDone && !nextReady)
            {
                SetNextReady(true);
                if (completePanel != null)
                    StartCoroutine(ShowCompleteCard());
            }
        }

        private System.Collections.IEnumerator ShowCompleteCard()
        {
            // 마지막 연출(몬스터 쓰러짐, 로켓 발사)이 끝날 시간을 준다.
            if (Config.stageCompleteDelaySeconds > 0f)
                yield return new WaitForSeconds(Config.stageCompleteDelaySeconds);

            string titleKey;
            object titleArg;
            int amount;
            GetCompleteContent(stage, out titleKey, out titleArg, out amount);
            completePanel.Show(titleKey, titleArg, amount);
        }

        /// <summary>완료 카드 내용: 제목(Loc 키 + 인자)과 아이콘 옆 개수.</summary>
        protected virtual void GetCompleteContent(StageBase s, out string titleKey, out object titleArg, out int amount)
        {
            titleKey = "common.goodJob";
            titleArg = null;
            amount = 0;
        }

        /// <summary>규칙에 넘기는 시계. 기본은 세션 시계, 3단계는 음악 재생 시계.</summary>
        protected virtual double StageTime()
        {
            return SessionManager.Time;
        }

        /// <summary>센서 연결 끊김으로 일시정지되거나 다시 시작될 때.</summary>
        protected virtual void OnPauseChanged(bool isPaused)
        {
        }

        /// <summary>이 화면의 규칙. 아직 없는 단계는 null.</summary>
        protected virtual StageBase CreateStage()
        {
            return null;
        }

        protected virtual void BindStage(StageBase s)
        {
        }

        protected virtual void UnbindStage(StageBase s)
        {
        }

        /// <summary>단계 시작 시 화면 초기화 (타이머/카운터 등).</summary>
        protected abstract void ResetView();

        /// <summary>매 프레임 화면 갱신. 값이 바뀔 때만 글자를 바꿀 것.</summary>
        protected virtual void UpdateView(StageBase s)
        {
        }

        /// <summary>게이지 목표 구간·점선 설정.</summary>
        protected abstract void ConfigureGauge(ForceGauge target);

        /// <summary>시연·개발용 즉시 완료 (F2).</summary>
        public void DebugComplete()
        {
            if (stage != null)
                stage.ForceComplete();
            else
                SetNextReady(true);
        }

        protected void SetNextReady(bool ready)
        {
            nextReady = ready;

            if (nextButton != null)
                nextButton.interactable = ready;
            if (nextBackground != null)
                nextBackground.color = ready ? Palette.Yellow : Palette.White;
            if (nextLabel != null)
                nextLabel.Label.color = ready ? Palette.ButtonText : Palette.DisabledText;
            if (nextShadow != null)
                nextShadow.enabled = ready;
        }

        private void RefreshMessage()
        {
            if (stage == null || messageText == null || stage.MessageVersion == shownMessageVersion)
                return;

            shownMessageVersion = stage.MessageVersion;
            messageText.Set(stage.MessageKey, stage.MessageArg);
        }

        private void OnNext()
        {
            if (!nextReady)
                return;

            if (stage != null)
                SessionManager.AddResult(stage.End());

            Go(NextScreen);
        }
    }
}
