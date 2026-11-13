using System;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/*
 * FSR 4개를 개인별로 보정하고 5회 운동을 진행한다.
 *
 * 실행 방법:
 * 1. RehabGameUI 씬의 GameController에 UdpSensorInput을 추가한다.
 * 2. Python sensor_bridge.py를 실행하고 Unity에서 Play를 누른다.
 * 3. 힘을 빼고 Record Rest를 누른다.
 * 4. 네 센서를 누른 상태에서 Record Max를 3번 누른다.
 * 5. 보정된 평균 힘 40~60%를 3초 유지하고 힘을 뺀다.
 *
 * UdpSensorInput이 없으면 기존 슬라이더로 화면을 시험할 수 있다.
 * 보정값은 실제 힘의 단위가 아닌 개인별 상대값이다.
 */
public class RehabGameUIController : MonoBehaviour
{
    private const int SensorCount = 4;

    [SerializeField] private Slider forceSlider;
    [SerializeField] private Button restButton;
    [SerializeField] private Button maxButton;
    [SerializeField] private TMP_Text forceText;
    [SerializeField] private TMP_Text repetitionText;
    [SerializeField] private TMP_Text messageText;

    // 센서 반응이 너무 작으면 보정 실패로 판단한다. 실측 후 조정 가능하다.
    [SerializeField] private int minimumSpan = 50;

    private UdpSensorInput sensorInput;
    private readonly int[] currentRaw = new int[SensorCount];
    private readonly int[] restRaw = new int[SensorCount];
    private readonly int[] maxRaw = new int[SensorCount];
    private readonly int[,] maxSamples = new int[3, SensorCount];

    private int maxCount;
    private int invalidSensorIndex = -1;
    private bool hasRest;
    private bool calibrated;

    private float heldTime;
    private int completed;
    private bool waitingForRelease;

    private bool InputReady =>
        sensorInput == null || sensorInput.HasFreshValue;

    private void Start()
    {
        if (forceSlider == null || restButton == null || maxButton == null ||
            forceText == null || repetitionText == null || messageText == null)
        {
            Debug.LogError("RehabGameUIController: UI 연결을 확인하세요.");
            enabled = false;
            return;
        }

        sensorInput = GetComponent<UdpSensorInput>();

        forceSlider.minValue = 0f;
        forceSlider.maxValue = 4095f;
        forceSlider.wholeNumbers = true;

        // 실제 센서가 있으면 가상 입력 슬라이더를 숨긴다.
        forceSlider.gameObject.SetActive(sensorInput == null);

        restButton.onClick.AddListener(RecordRest);
        maxButton.onClick.AddListener(RecordMax);

        ReadInput();
        UpdateUI();
    }

    private void OnDestroy()
    {
        if (restButton != null)
            restButton.onClick.RemoveListener(RecordRest);

        if (maxButton != null)
            maxButton.onClick.RemoveListener(RecordMax);
    }

    private void Update()
    {
        ReadInput();

        // 수신이 끊기면 운동 유지 시간을 진행하지 않는다.
        if (!InputReady)
        {
            heldTime = 0f;
            UpdateUI();
            return;
        }

        if (calibrated && completed < 5)
        {
            float average = AveragePercent();

            if (waitingForRelease)
            {
                if (average <= 20f)
                    waitingForRelease = false;
            }
            else if (average >= 40f && average <= 60f)
            {
                heldTime += Time.deltaTime;

                if (heldTime >= 3f)
                {
                    completed++;
                    heldTime = 0f;
                    waitingForRelease = true;
                }
            }
            else
            {
                heldTime = 0f;
            }
        }

        UpdateUI();
    }

    private void ReadInput()
    {
        if (sensorInput != null)
        {
            if (!sensorInput.HasFreshValue)
                return;

            for (int i = 0; i < SensorCount; i++)
                currentRaw[i] = sensorInput.GetRawValue(i);
        }
        else
        {
            // 하드웨어가 없을 때는 슬라이더 하나를 네 센서에 적용한다.
            int simulatedRaw = Mathf.RoundToInt(forceSlider.value);

            for (int i = 0; i < SensorCount; i++)
                currentRaw[i] = simulatedRaw;
        }
    }

    private float SensorPercent(int index)
    {
        if (!calibrated)
            return 0f;

        // 눌렀을 때 원시값이 증가하거나 감소해도 같은 방식으로 계산된다.
        return Mathf.Clamp01(
            (float)(currentRaw[index] - restRaw[index]) /
            (maxRaw[index] - restRaw[index])
        ) * 100f;
    }

    private float AveragePercent()
    {
        float total = 0f;

        for (int i = 0; i < SensorCount; i++)
            total += SensorPercent(i);

        return total / SensorCount;
    }

    private void RecordRest()
    {
        if (!InputReady)
            return;

        for (int i = 0; i < SensorCount; i++)
            restRaw[i] = currentRaw[i];

        hasRest = true;
        calibrated = false;
        maxCount = 0;
        invalidSensorIndex = -1;
        heldTime = 0f;
        completed = 0;
        waitingForRelease = false;
        UpdateUI();
    }

    private void RecordMax()
    {
        if (!InputReady || !hasRest || maxCount >= 3)
            return;

        for (int i = 0; i < SensorCount; i++)
            maxSamples[maxCount, i] = currentRaw[i];

        maxCount++;

        if (maxCount == 3)
        {
            for (int i = 0; i < SensorCount; i++)
            {
                int[] samples =
                {
                    maxSamples[0, i],
                    maxSamples[1, i],
                    maxSamples[2, i]
                };

                Array.Sort(samples);
                maxRaw[i] = samples[1];

                if (Mathf.Abs(maxRaw[i] - restRaw[i]) < minimumSpan &&
                    invalidSensorIndex == -1)
                    invalidSensorIndex = i;
            }

            calibrated = invalidSensorIndex == -1;
        }

        UpdateUI();
    }

    private void UpdateUI()
    {
        if (forceText == null || repetitionText == null ||
            messageText == null || restButton == null || maxButton == null)
            return;

        restButton.interactable = InputReady;
        maxButton.interactable = InputReady && hasRest && maxCount < 3;
        repetitionText.text = $"Completed: {completed}/5";

        if (!InputReady)
        {
            forceText.text = "Waiting for four sensors";
            messageText.text = "Start sensor_bridge.py";
            return;
        }

        StringBuilder display = new StringBuilder();

        for (int i = 0; i < SensorCount; i++)
        {
            if (calibrated)
                display.AppendLine($"S{i + 1}: {SensorPercent(i):F0}%");
            else
                display.AppendLine($"S{i + 1} raw: {currentRaw[i]}");
        }

        if (calibrated)
            display.Append($"Average: {AveragePercent():F0}%");

        forceText.text = display.ToString();

        if (!hasRest)
            messageText.text = "Relax, then press Record Rest";
        else if (maxCount < 3)
            messageText.text = $"Press four sensors: Record Max ({maxCount}/3)";
        else if (invalidSensorIndex >= 0)
            messageText.text =
                $"S{invalidSensorIndex + 1} range too small. Record Rest again";
        else if (completed >= 5)
            messageText.text = "Session complete! Record Rest to restart";
        else if (waitingForRelease)
            messageText.text = "Good! Release below 20%";
        else
            messageText.text = $"Hold average 40-60%: {heldTime:F1}/3.0 s";
    }
}