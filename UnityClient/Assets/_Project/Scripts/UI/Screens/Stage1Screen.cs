using System.Collections;
using Malang.Core;
using Malang.Training;
using UnityEngine;
using UnityEngine.UI;

namespace Malang.UI
{
    /// <summary>
    /// 1단계 · 몬스터 격파 화면.
    /// 규칙은 Stage1Monster, 화면은 몬스터 등장(팝)·피격(움찔 + 번쩍) 후 사라짐, 처치 수, 남은 시간.
    /// </summary>
    public class Stage1Screen : StageScreenBase
    {
        private const float HitSquash = 0.85f;
        private const float HitTilt = -6f;
        private const float HitPeak = 0.3f;   // 피격 애니메이션 중 가장 찌그러지는 지점 (비율)

        [SerializeField] private LocText killCountText;
        [SerializeField] private RectTransform monster;
        [SerializeField] private Image monsterBody;
        [SerializeField] private CanvasGroup monsterGroup;
        [SerializeField] private GameObject eyesNormal;
        [SerializeField] private GameObject eyesDefeated;
        [SerializeField] private RectTransform bush;
        [SerializeField] private RectTransform alertMark;

        private Color bodyColor;
        private int shownSeconds = -1;
        private int shownKills = -1;
        private Coroutine monsterRoutine;
        private Coroutine telegraphRoutine;

        public override ScreenId Id => ScreenId.Stage1;
        protected override ScreenId NextScreen => ScreenId.Stage2;

        protected override void Awake()
        {
            base.Awake();
            if (monsterBody != null)
                bodyColor = monsterBody.color;
        }

        protected override StageBase CreateStage()
        {
            return new Stage1Monster();
        }

        protected override void BindStage(StageBase s)
        {
            var m = (Stage1Monster)s;
            m.Spawned += OnSpawned;
            m.Hit += OnHit;
            m.TelegraphStarted += OnTelegraphStarted;
            m.TelegraphCanceled += OnTelegraphCanceled;
        }

        protected override void UnbindStage(StageBase s)
        {
            var m = (Stage1Monster)s;
            m.Spawned -= OnSpawned;
            m.Hit -= OnHit;
            m.TelegraphStarted -= OnTelegraphStarted;
            m.TelegraphCanceled -= OnTelegraphCanceled;
        }

        protected override void ResetView()
        {
            shownSeconds = -1;
            shownKills = -1;
            monsterRoutine = null;
            telegraphRoutine = null;
            ShowMonster(false);
            ResetTelegraph();
            counterText.SetRaw(IntStrings.Get(Mathf.CeilToInt(Config.s1TimeLimitSeconds)));
            SetKills(0);
        }

        protected override void UpdateView(StageBase s)
        {
            var m = (Stage1Monster)s;

            int seconds = Mathf.CeilToInt(m.RemainingSeconds);
            if (seconds != shownSeconds)
            {
                shownSeconds = seconds;
                counterText.SetRaw(IntStrings.Get(seconds));
            }

            if (m.Kills != shownKills)
                SetKills(m.Kills);

            // 게이지 말풍선: 몬스터가 나와 있으면 쥐기 구간,
            // 처치 후·쉬기·예고 중이거나 쥔 채로 몬스터가 나왔으면 쉬기 구간
            GaugeTarget target = GaugeTarget.None;
            if (!m.IsDone)
            {
                bool relax = m.NeedsRelease || m.BlockedByPreGrip;
                if (relax && Config.s1ShowRelaxCallout)
                    target = GaugeTarget.RelaxZone;
                else if (m.MonsterOut && !m.BlockedByPreGrip)
                    target = GaugeTarget.GripZone;
            }
            gauge.SetTarget(target);

            // 시간이 끝나 남아 있던 몬스터·예고는 치운다.
            if (m.IsDone && !m.MonsterOut && monsterRoutine == null && monster.gameObject.activeSelf)
                ShowMonster(false);
            if (m.IsDone && telegraphRoutine != null)
                StopTelegraph();
        }

        protected override void ConfigureGauge(ForceGauge target)
        {
            // 1단계: 쥐기 구간(50 이상)·쉬기 구간(20 이하)을 칠하고 경계에 점선
            target.ClearBand();
            target.SetZones(Config.gripThreshold, Config.releaseThreshold);
            target.SetTarget(GaugeTarget.None);
        }

        protected override void GetCompleteContent(StageBase s, out string titleKey, out object titleArg, out int amount)
        {
            var m = (Stage1Monster)s;
            titleKey = m.Kills >= m.TargetKills ? Stage1Monster.MsgClear : "common.goodJob";
            titleArg = null;
            amount = m.Kills;
        }

        private void SetKills(int kills)
        {
            shownKills = kills;
            killCountText.SetRaw(IntStrings.Get(kills) + " / " + IntStrings.Get(Config.s1TargetKills));
        }

        private void ShowMonster(bool visible)
        {
            monster.gameObject.SetActive(visible);
            monster.localScale = Vector3.one;
            monster.localRotation = Quaternion.identity;
            monsterBody.color = bodyColor;
            monsterGroup.alpha = 1f;
            SetDefeatedFace(false);
        }

        private void SetDefeatedFace(bool defeated)
        {
            eyesNormal.SetActive(!defeated);
            eyesDefeated.SetActive(defeated);
        }

        private void OnSpawned()
        {
            StopTelegraph();
            Play(SpawnRoutine());
        }

        // ---------- 등장 예고: 덤불 흔들림 + "!" ----------

        private void OnTelegraphStarted()
        {
            StopTelegraph();
            telegraphRoutine = StartCoroutine(TelegraphRoutine());
        }

        private void OnTelegraphCanceled()
        {
            StopTelegraph();
        }

        private void StopTelegraph()
        {
            if (telegraphRoutine != null)
                StopCoroutine(telegraphRoutine);
            telegraphRoutine = null;
            ResetTelegraph();
        }

        private void ResetTelegraph()
        {
            bush.localRotation = Quaternion.identity;
            alertMark.gameObject.SetActive(false);
            alertMark.localScale = Vector3.one;
        }

        /// <summary>
        /// 예고: 덤불이 살살 흔들리기 시작해 점점 세게 → 중간(s1AlertAtFraction)에 "!" 가 톡 튀어나옴.
        /// 몬스터가 나오면 OnSpawned 가, 예고가 취소되면 OnTelegraphCanceled 가 멈춘다.
        /// </summary>
        private IEnumerator TelegraphRoutine()
        {
            float duration = Mathf.Max(0.01f, Config.s1TelegraphSeconds);
            float alertAt = duration * Config.s1AlertAtFraction;
            float pop = Mathf.Max(0.01f, Config.s1SpawnAnimSeconds);
            alertMark.gameObject.SetActive(false);

            for (float t = 0f; ; t += Time.deltaTime)
            {
                float progress = Mathf.Clamp01(t / duration);
                float strength = Mathf.Lerp(Config.s1BushShakeStart, 1f, progress);
                float angle = strength * Config.s1BushShakeDegrees *
                              Mathf.Sin(t * Config.s1BushShakeHz * 2f * Mathf.PI);
                bush.localRotation = Quaternion.Euler(0f, 0f, angle);

                if (t >= alertAt)
                {
                    if (!alertMark.gameObject.activeSelf)
                        alertMark.gameObject.SetActive(true);

                    // 톡 튀어나와 살짝 커졌다가 제 크기로
                    float k = (t - alertAt) / pop;
                    float s = k < 1f ? Mathf.Lerp(0f, 1.2f, k) : (k < 1.5f ? Mathf.Lerp(1.2f, 1f, (k - 1f) * 2f) : 1f);
                    alertMark.localScale = new Vector3(s, s, 1f);
                }

                yield return null;
            }
        }

        private void OnHit()
        {
            Play(HitRoutine());
        }

        private void Play(IEnumerator routine)
        {
            if (monsterRoutine != null)
                StopCoroutine(monsterRoutine);
            monsterRoutine = StartCoroutine(routine);
        }

        /// <summary>등장: 작게 시작해 살짝 커졌다가 제 크기로.</summary>
        private IEnumerator SpawnRoutine()
        {
            ShowMonster(true);
            float duration = Mathf.Max(0.01f, Config.s1SpawnAnimSeconds);

            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                float k = t / duration;
                float scale = k < 0.7f ? Mathf.Lerp(0f, 1.1f, k / 0.7f) : Mathf.Lerp(1.1f, 1f, (k - 0.7f) / 0.3f);
                monster.localScale = new Vector3(scale, scale, 1f);
                yield return null;
            }

            monster.localScale = Vector3.one;
            monsterRoutine = null;
        }

        /// <summary>
        /// 피격: 움찔(0.85배, -6°) + 밝게 번쩍 (시안 .mon.hit, s1HitAnimSeconds)
        /// → × 눈으로 잠깐 멈춤 (s1DefeatHoldSeconds)
        /// → 작아지며 흐려져 사라짐 (s1DefeatFadeSeconds)
        /// 쓰러지는 도중 다음 몬스터가 나오면 SpawnRoutine 이 이 연출을 끊고 새로 시작한다.
        /// </summary>
        private IEnumerator HitRoutine()
        {
            float duration = Mathf.Max(0.01f, Config.s1HitAnimSeconds);
            Color flash = Color.Lerp(bodyColor, Color.white, 0.6f);

            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                float k = t / duration;
                float w = k < HitPeak ? k / HitPeak : 1f - (k - HitPeak) / (1f - HitPeak);
                float scale = Mathf.Lerp(1f, HitSquash, w);
                monster.localScale = new Vector3(scale, scale, 1f);
                monster.localRotation = Quaternion.Euler(0f, 0f, HitTilt * w);
                monsterBody.color = Color.Lerp(bodyColor, flash, w);
                yield return null;
            }

            monster.localScale = Vector3.one;
            monster.localRotation = Quaternion.identity;
            monsterBody.color = bodyColor;
            SetDefeatedFace(true);

            if (Config.s1DefeatHoldSeconds > 0f)
                yield return new WaitForSeconds(Config.s1DefeatHoldSeconds);

            float fade = Config.s1DefeatFadeSeconds;
            for (float t = 0f; t < fade; t += Time.deltaTime)
            {
                float k = t / fade;
                float scale = Mathf.Lerp(1f, Config.s1DefeatEndScale, k);
                monster.localScale = new Vector3(scale, scale, 1f);
                monsterGroup.alpha = 1f - k;
                yield return null;
            }

            ShowMonster(false);
            monsterRoutine = null;
        }
    }
}
