using System.Collections;
using Malang.Core;
using Malang.Training;
using UnityEngine;
using UnityEngine.UI;

namespace Malang.UI
{
    /// <summary>
    /// 2단계 · 파워 차징 화면.
    /// 규칙은 Stage2Charge. 화면:
    /// - 로켓이 서 있다(받침대 없음). 진행 링(남은 초), 발사 수.
    /// - 불꽃: 구간 밖에서는 흐림, 구간 안에서 밝게 일렁임.
    /// - 발사: 점화(부르르 + 불꽃 커짐 + 바닥 연기) → 이륙(가속 + 긴 불꽃 + 연기 꼬리).
    /// - 힘을 빼서 새 시행이 시작되면 새 로켓이 위에서 불을 뿜으며 내려와 착지한다.
    /// </summary>
    public class Stage2Screen : StageScreenBase
    {
        private const float FlickerHz = 9f;
        private const float FlickerAmount = 0.12f;
        private const float LaunchFlameScale = 1.9f;
        private const float LandFlameScale = 1.4f;
        private const float ShakePixels = 7f;
        private const float ShakeHz = 30f;
        private const float TrailInterval = 0.04f;
        private const float LandBounce = 10f;

        [SerializeField] private Image ringFill;
        [SerializeField] private LocText ringText;
        [SerializeField] private RectTransform rocket;
        [SerializeField] private RectTransform flame;
        [SerializeField] private CanvasGroup flameGroup;
        [SerializeField] private SmokePuffs smoke;

        private Vector2 rocketHome;
        private int shownLaunches = -1;
        private int shownSecondsLeft = -1;
        private bool launching;
        private bool rocketOnPad;
        private bool landRequested;

        public override ScreenId Id => ScreenId.Stage2;
        protected override ScreenId NextScreen => ScreenId.Stage3;

        /// <summary>화면 위 바깥 (새 로켓이 내려오기 시작하는 곳, 날아간 로켓이 사라지는 곳).</summary>
        private Vector2 SkyPosition => rocketHome + new Vector2(0f, Config.s2RocketFlyHeight);

        protected override void Awake()
        {
            base.Awake();
            rocketHome = rocket.anchoredPosition;
        }

        protected override StageBase CreateStage()
        {
            return new Stage2Charge();
        }

        protected override void BindStage(StageBase s)
        {
            var c = (Stage2Charge)s;
            c.Launched += OnLaunched;
            c.TrialStarted += OnTrialStarted;
        }

        protected override void UnbindStage(StageBase s)
        {
            var c = (Stage2Charge)s;
            c.Launched -= OnLaunched;
            c.TrialStarted -= OnTrialStarted;
        }

        protected override void ResetView()
        {
            launching = false;
            landRequested = false;
            rocketOnPad = true;
            SetRocketPosition(rocketHome);
            smoke.Clear();
            ringFill.fillAmount = 0f;
            shownSecondsLeft = -1;
            SetSecondsLeft(Mathf.CeilToInt(Config.s2HoldSeconds));
            SetFlame(false, 1f);
            SetLaunches(0);
        }

        protected override void UpdateView(StageBase s)
        {
            var c = (Stage2Charge)s;

            if (!launching && rocketOnPad)
            {
                ringFill.fillAmount = c.HoldProgress01;
                SetSecondsLeft(c.HoldSecondsLeft);

                // 구간 안에서 버티는 동안 불꽃이 살짝 일렁인다.
                float flicker = 1f + FlickerAmount * Mathf.Sin(Time.time * FlickerHz * 2f * Mathf.PI);
                SetFlame(c.FlameOn, c.FlameOn ? flicker : 1f);
            }

            if (c.Launches != shownLaunches)
                SetLaunches(c.Launches);

            // 게이지 말풍선 "여기!": 로켓이 서 있고 충전 중일 때만
            bool charging = !c.IsDone && !launching && rocketOnPad && !c.WaitingForRelease;
            gauge.SetTarget(charging ? GaugeTarget.Band : GaugeTarget.None);
        }

        protected override void ConfigureGauge(ForceGauge target)
        {
            // M4: 적응형 난이도가 정한 폭(StageConfig.S2BandWidth)으로 바뀐다.
            var c = (Stage2Charge)Stage;
            target.ClearZones();
            target.SetBand(c.Low, c.High);
            target.SetTarget(GaugeTarget.Band);
        }

        protected override void GetCompleteContent(StageBase s, out string titleKey, out object titleArg, out int amount)
        {
            var c = (Stage2Charge)s;
            titleKey = c.Launches >= c.TargetLaunches ? "s2.clearTitle" : "common.goodJob";
            titleArg = null;
            amount = c.Launches;
        }

        private void SetLaunches(int launches)
        {
            shownLaunches = launches;
            counterText.SetArgs(launches, Config.s2LaunchTarget);
        }

        private void SetSecondsLeft(int seconds)
        {
            if (seconds == shownSecondsLeft)
                return;
            shownSecondsLeft = seconds;
            ringText.SetRaw(IntStrings.Get(seconds));
        }

        /// <summary>불꽃 밝기(켜짐/흐림)와 길이(세로 배율).</summary>
        private void SetFlame(bool on, float lengthScale)
        {
            flameGroup.alpha = on ? 1f : Config.s2FlameOffAlpha;
            flame.localScale = new Vector3(1f, lengthScale, 1f);
        }

        private void SetRocketPosition(Vector2 position)
        {
            rocket.anchoredPosition = position;
        }

        /// <summary>로켓 노즐 위치 (연기 오브젝트 기준 좌표).</summary>
        private Vector2 NozzlePosition()
        {
            return smoke.transform.InverseTransformPoint(flame.position);
        }

        // ---------- 발사 ----------

        private void OnLaunched()
        {
            StartCoroutine(LaunchRoutine());
        }

        private IEnumerator LaunchRoutine()
        {
            launching = true;
            ringFill.fillAmount = 1f;
            SetSecondsLeft(0);

            // 1) 점화: 부르르 떨리며 불꽃이 커지고 바닥에 연기가 양옆으로 퍼진다.
            float ignition = Mathf.Max(0.01f, Config.s2IgnitionSeconds);
            EmitGroundSmoke();
            for (float t = 0f; t < ignition; t += Time.deltaTime)
            {
                float k = t / ignition;
                float shake = ShakePixels * Mathf.Sin(t * ShakeHz * 2f * Mathf.PI);
                SetRocketPosition(rocketHome + new Vector2(shake, 0f));
                SetFlame(true, Mathf.Lerp(1f, LaunchFlameScale, k) * Flicker(t));
                yield return null;
            }

            // 2) 이륙: 가속하며 날아오르고 긴 불꽃이 일렁이며 연기 꼬리를 남긴다.
            float fly = Mathf.Max(0.01f, Config.s2RocketFlySeconds);
            float nextPuff = 0f;
            for (float t = 0f; t < fly; t += Time.deltaTime)
            {
                float k = t / fly;
                SetRocketPosition(rocketHome + new Vector2(0f, Config.s2RocketFlyHeight * k * k * k));
                SetFlame(true, LaunchFlameScale * Flicker(t));

                if (t >= nextPuff)
                {
                    nextPuff = t + TrailInterval;
                    smoke.Emit(NozzlePosition() + new Vector2(Random.Range(-12f, 12f), 0f),
                        new Vector2(Random.Range(-40f, 40f), -90f), Random.Range(60f, 95f), Random.Range(0.5f, 0.75f));
                }

                yield return null;
            }

            // 3) 로켓은 화면 위 바깥에 둔다. 힘을 빼면 새 로켓이 거기서 내려온다.
            rocketOnPad = false;
            SetRocketPosition(SkyPosition);
            SetFlame(false, 1f);
            ringFill.fillAmount = 0f;

            float rest = Config.s2LaunchSequenceSeconds - ignition - fly;
            if (rest > 0f)
                yield return new WaitForSeconds(rest);

            launching = false;

            if (landRequested)
            {
                landRequested = false;
                StartCoroutine(LandRoutine());
            }
        }

        private void OnTrialStarted(int trial)
        {
            if (rocketOnPad)
                return; // 첫 시행, 또는 시간 초과로 실패해 로켓이 아직 서 있음

            if (launching)
                landRequested = true; // 발사 연출이 끝나면 내려보낸다
            else
                StartCoroutine(LandRoutine());
        }

        /// <summary>새 로켓이 위에서 불을 뿜으며 내려와(점점 느려짐) 착지하고 살짝 통 튄다.</summary>
        private IEnumerator LandRoutine()
        {
            SetSecondsLeft(Mathf.CeilToInt(Config.s2HoldSeconds));
            float duration = Mathf.Max(0.01f, Config.s2RocketLandSeconds);
            Vector2 from = SkyPosition;

            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                float k = t / duration;
                float ease = 1f - (1f - k) * (1f - k) * (1f - k); // 점점 느려짐
                SetRocketPosition(Vector2.Lerp(from, rocketHome, ease));
                SetFlame(true, LandFlameScale * Flicker(t));
                yield return null;
            }

            // 착지: 살짝 통 튀고 불꽃은 꺼진다.
            SetFlame(false, 1f);
            float bounce = Mathf.Max(0.01f, Config.s2LandBounceSeconds);
            for (float t = 0f; t < bounce; t += Time.deltaTime)
            {
                float k = t / bounce;
                SetRocketPosition(rocketHome + new Vector2(0f, LandBounce * Mathf.Sin(k * Mathf.PI)));
                yield return null;
            }

            SetRocketPosition(rocketHome);
            rocketOnPad = true;
        }

        private void EmitGroundSmoke()
        {
            Vector2 nozzle = NozzlePosition();
            for (int i = 0; i < 8; i++)
            {
                float side = i % 2 == 0 ? -1f : 1f;
                smoke.Emit(nozzle + new Vector2(side * Random.Range(10f, 40f), Random.Range(-30f, -10f)),
                    new Vector2(side * Random.Range(160f, 320f), Random.Range(10f, 60f)),
                    Random.Range(80f, 120f), Random.Range(0.8f, 1.1f));
            }
        }

        private static float Flicker(float t)
        {
            return 1f + FlickerAmount * Mathf.Sin(t * FlickerHz * 2f * Mathf.PI) + Random.Range(-0.05f, 0.05f);
        }
    }
}
