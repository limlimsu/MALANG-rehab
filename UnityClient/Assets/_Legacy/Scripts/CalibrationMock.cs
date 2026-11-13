using System;
using UnityEngine;

// 센서 없이 개인별 힘 보정 흐름을 시험하는 화면 (테스팅)
// 슬라이더가 압력센서 원시값을 대신하며, 추후 실제 센서 입력으로 교체한다.
/*
 * 용도: 실제 센서 없이 개인별 힘 보정 과정을 시험한다.
 *
 * 실행 방법
 * 1. Unity에서 Assets/Scenes/CalibrationTest 씬을 연다.
 * 2. 위쪽 Play 버튼을 누르고 Game 탭을 본다.
 * 3. 슬라이더를 왼쪽으로 옮긴 뒤 "휴식값으로 기록"을 누른다.
 * 4. 슬라이더를 오른쪽으로 옮겨 "최대 수행값 기록"을 3번 누른다.
 * 5. 슬라이더를 움직이며 보정값이 0~100%로 변하는지 확인한다.
 *
 * 보정값은 게임 입력용 상대값이며 실제 악력 단위가 아니다.
 */
public class CalibrationMock : MonoBehaviour
{
    private float rawValue;
    private float restValue;
    private readonly float[] maxSamples = new float[3];
    private int sampleCount;
    private bool hasRest;
    private bool calibrated;
    private float maxValue;

    // 다른 게임 스크립트가 보정 완료 여부와 현재 힘(0~100%)을 읽을 때 사용한다.
    public bool IsCalibrated => calibrated;

    public float CurrentPercent =>
        calibrated
            ? Mathf.Clamp01((rawValue - restValue) / (maxValue - restValue)) * 100f
            : 0f;
    private void OnGUI()
    {
        GUILayout.BeginArea(new Rect(20, 20, 420, 320), GUI.skin.box);

        GUILayout.Label($"가상 센서 원시값: {rawValue:F0}");
        rawValue = GUILayout.HorizontalSlider(rawValue, 0f, 4095f);

        if (GUILayout.Button("1. 현재 값을 휴식값으로 기록"))
        {
            restValue = rawValue;
            hasRest = true;
            calibrated = false;
            sampleCount = 0;
        }

        if (hasRest && sampleCount < 3 &&
            GUILayout.Button($"2. 최대 수행값 기록 ({sampleCount + 1}/3)"))
        {
            maxSamples[sampleCount] = rawValue;
            sampleCount++;

            if (sampleCount == 3)
            {
                // 세 번의 최대 수행값 중 중앙값을 사용해 한 번의 튀는 측정값 영향을 줄인다.
                Array.Sort(maxSamples);
                maxValue = maxSamples[1]; // 세 값의 중앙값
                calibrated = maxValue - restValue >= 100f;
            }
        }

        GUILayout.Label($"휴식값: {(hasRest ? restValue.ToString("F0") : "미측정")}");
        GUILayout.Label($"최대 수행값: {sampleCount}/3회 기록");

        if (sampleCount == 3 && !calibrated)
            GUILayout.Label("측정 범위가 너무 작습니다. 휴식값부터 다시 측정하세요.");

        if (calibrated)
        {
            // 개인별 휴식값을 0%, 최대 수행값을 100%로 환산한다.
            // 이는 실제 악력 단위가 아닌 게임 입력용 상대값이다.
            float percent = Mathf.Clamp01(
                (rawValue - restValue) / (maxValue - restValue)
            ) * 100f;

            GUILayout.Label($"개인별 보정값: {percent:F1}%");
            GUILayout.Label($"기준: {restValue:F0} / 최대: {maxValue:F0}");
        }

        GUILayout.EndArea();
    }
}