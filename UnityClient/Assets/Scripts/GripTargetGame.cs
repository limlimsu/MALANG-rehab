using UnityEngine;

/*
 * 가상 압력센서로 목표 힘 유지 게임을 시험한다.
 *
 * 실행 방법:
 * 1. GripGameTest 씬의 GameController에 CalibrationMock과
 *    GripTargetGame 스크립트를 모두 추가한다.
 * 2. Play를 누르고 휴식값과 최대 수행값 3회를 기록한다.
 * 3. 슬라이더로 보정값 40~60%를 3초간 유지한다.
 */
public class GripTargetGame : MonoBehaviour
{
    private CalibrationMock calibration;
    private float heldTime;
    private bool succeeded;

    private void Awake()
    {
        calibration = GetComponent<CalibrationMock>();
    }

    private void Update()
    {
        if (calibration == null || !calibration.IsCalibrated || succeeded)
            return;

        float percent = calibration.CurrentPercent;

        // 목표 범위를 벗어나면 유지 시간을 처음부터 다시 센다.
        if (percent >= 40f && percent <= 60f)
            heldTime += Time.deltaTime;
        else
            heldTime = 0f;

        if (heldTime >= 3f)
            succeeded = true;
    }

    private void OnGUI()
    {
        GUILayout.BeginArea(new Rect(460, 20, 350, 200), GUI.skin.box);
        GUILayout.Label("목표: 보정된 힘 40~60%를 3초간 유지");

        if (calibration == null)
            GUILayout.Label("GameController에 CalibrationMock을 추가하세요.");
        else if (!calibration.IsCalibrated)
            GUILayout.Label("먼저 왼쪽에서 개인별 힘 보정을 완료하세요.");
        else
        {
            GUILayout.Label($"현재 힘: {calibration.CurrentPercent:F1}%");
            GUILayout.Label($"유지 시간: {Mathf.Min(heldTime, 3f):F1} / 3.0초");
            GUILayout.Label(succeeded ? "성공!" : "목표 범위에 맞춰 주세요.");
        }

        GUILayout.EndArea();
    }
}