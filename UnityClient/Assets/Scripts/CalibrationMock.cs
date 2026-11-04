using System;
using UnityEngine;

public class CalibrationMock : MonoBehaviour
{
    private float rawValue;
    private float restValue;
    private readonly float[] maxSamples = new float[3];
    private int sampleCount;
    private bool hasRest;
    private bool calibrated;
    private float maxValue;

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
            float percent = Mathf.Clamp01(
                (rawValue - restValue) / (maxValue - restValue)
            ) * 100f;

            GUILayout.Label($"개인별 보정값: {percent:F1}%");
            GUILayout.Label($"기준: {restValue:F0} / 최대: {maxValue:F0}");
        }

        GUILayout.EndArea();
    }
}