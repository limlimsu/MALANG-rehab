using UnityEngine;

/*
 * 개인별 보정값으로 5회 반복하는 목표 힘 유지 게임.
 *
 * 실행 방법:
 * 1. GripGameTest 씬에서 Play를 누른다.
 * 2. 왼쪽 화면에서 휴식값과 최대 수행값 3회를 기록한다.
 * 3. 보정된 힘 40~60%를 3초 유지한다.
 * 4. 힘을 20% 이하로 빼고 다음 회차를 시작한다.
 * 5. 총 5회를 완료하면 결과가 표시된다.
 *
 * 현재 입력은 CalibrationMock의 가상 슬라이더를 사용한다.
 */
public class GripTargetGame : MonoBehaviour
{
    private const float TargetMinPercent = 40f;
    private const float TargetMaxPercent = 60f;
    private const float ReleasePercent = 20f;
    private const float RequiredHoldSeconds = 3f;
    private const int TotalRepetitions = 5;

    private CalibrationMock calibration;
    private float heldTime;
    private int completedRepetitions;
    private bool waitingForRelease;

    private void Awake()
    {
        calibration = GetComponent<CalibrationMock>();
    }

    private void Update()
    {
        if (calibration == null || !calibration.IsCalibrated ||
            completedRepetitions >= TotalRepetitions)
            return;

        float percent = calibration.CurrentPercent;

        // 한 회차가 끝나면 힘을 뺀 뒤 다음 회차를 시작한다.
        if (waitingForRelease)
        {
            if (percent <= ReleasePercent)
                waitingForRelease = false;

            return;
        }

        // 목표 범위를 벗어나면 이번 회차의 유지 시간을 다시 센다.
        if (percent >= TargetMinPercent && percent <= TargetMaxPercent)
            heldTime += Time.deltaTime;
        else
            heldTime = 0f;

        if (heldTime >= RequiredHoldSeconds)
        {
            completedRepetitions++;
            heldTime = 0f;
            waitingForRelease = true;
        }
    }

    private void OnGUI()
    {
        GUILayout.BeginArea(new Rect(460, 20, 350, 240), GUI.skin.box);
        GUILayout.Label("목표: 보정된 힘 40~60%를 3초간 유지");

        if (calibration == null)
        {
            GUILayout.Label("GameController에 CalibrationMock을 추가하세요.");
        }
        else if (!calibration.IsCalibrated)
        {
            GUILayout.Label("먼저 왼쪽에서 개인별 힘 보정을 완료하세요.");
        }
        else
        {
            GUILayout.Label($"현재 힘: {calibration.CurrentPercent:F1}%");
            GUILayout.Label($"완료: {completedRepetitions}/{TotalRepetitions}회");

            if (completedRepetitions >= TotalRepetitions)
            {
                GUILayout.Label("재활 세션 완료!");

                if (GUILayout.Button("다시 시작"))
                    RestartSession();
            }
            else if (waitingForRelease)
            {
                GUILayout.Label("성공! 힘을 20% 이하로 빼세요.");
            }
            else
            {
                GUILayout.Label(
                    $"유지 시간: {Mathf.Min(heldTime, RequiredHoldSeconds):F1} / " +
                    $"{RequiredHoldSeconds:F1}초"
                );
                GUILayout.Label("목표 범위에 맞춰 유지하세요.");
            }
        }

        GUILayout.EndArea();
    }

    private void RestartSession()
    {
        heldTime = 0f;
        completedRepetitions = 0;
        waitingForRelease = false;
    }
}