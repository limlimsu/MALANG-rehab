using Malang.Core;
using Malang.Training;
using UnityEngine;
using UnityEngine.UI;

namespace Malang.UI
{
    /// <summary>
    /// 이번 훈련의 세 단계 성공 횟수와 주요 지표를 보여 주는 결과 화면.
    /// shortcut: 현재 세션만 표시한다. 이력 저장소와 사용자 계정이 생기면 기간별 기록으로 확장한다.
    /// </summary>
    public class ResultScreen : ScreenBase
    {
        [SerializeField] private Button homeButton;
        [SerializeField] private LocText[] scoreTexts;
        [SerializeField] private LocText[] metricTexts;

        public override ScreenId Id => ScreenId.Result;

        private void Awake()
        {
            if (homeButton != null)
                homeButton.onClick.AddListener(OnHome);
        }

        private void OnDestroy()
        {
            if (homeButton != null)
                homeButton.onClick.RemoveListener(OnHome);
        }

        private void OnHome()
        {
            Go(ScreenId.Main);
        }

        public override void OnEnter()
        {
            SessionManager.End();
            if (scoreTexts == null || scoreTexts.Length < 3 || metricTexts == null || metricTexts.Length < 3)
                return;

            for (int i = 0; i < scoreTexts.Length; i++)
                scoreTexts[i].SetRaw("0 / 0");

            metricTexts[0].Set("done.metricS1", "—");
            metricTexts[1].Set("done.metricS2", "—");
            metricTexts[2].Set("done.metricS3", "—");

            foreach (StageResult result in SessionManager.Results)
            {
                if (result.stage >= 1 && result.stage <= 3)
                    scoreTexts[result.stage - 1].SetRaw(result.score + " / " + result.tries);

                if (result.stage == 1)
                    metricTexts[0].Set("done.metricS1", Metric(result.reactionMsMean, "0", " ms"));
                else if (result.stage == 2)
                    metricTexts[1].Set("done.metricS2", Metric(result.accuracyMae, "0.0", " %p"));
                else if (result.stage == 3)
                    metricTexts[2].Set("done.metricS3", Metric(result.timingErrMs, "0", " ms"));
            }
        }

        private static string Metric(float value, string format, string unit)
        {
            return value == StageResult.None ? "—" : value.ToString(format) + unit;
        }
    }
}
