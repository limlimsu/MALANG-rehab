using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/*
 * RehabGameUI 씬의 가상 센서 시제품.
 *
 * 실행 방법:
 * 1. GameController에 이 스크립트를 추가한다.
 * 2. Inspector에서 슬라이더, 버튼 2개, 글자 3개를 연결한다.
 * 3. Play → Record Rest → Record Max 3회 순서로 보정한다.
 * 4. 보정된 힘 40~60%를 3초 유지하고 20% 이하로 힘을 뺀다.
 * 5. 총 5회를 완료한다.
 *
 * ForceSlider는 실제 센서 대신 사용하는 가상 입력이다.
 */
public class RehabGameUIController : MonoBehaviour
{
    [SerializeField] private Slider forceSlider;
    [SerializeField] private Button restButton;
    [SerializeField] private Button maxButton;
    [SerializeField] private TMP_Text forceText;
    [SerializeField] private TMP_Text repetitionText;
    [SerializeField] private TMP_Text messageText;

    private readonly float[] maxSamples = new float[3];
    private float restRaw;
    private float maxRaw;
    private int maxCount;
    private bool hasRest;
    private bool calibrated;
    private bool invalidRange;

    private float heldTime;
    private int completed;
    private bool waitingForRelease;

    private void Start()
    {
        if (forceSlider == null || restButton == null || maxButton == null ||
            forceText == null || repetitionText == null || messageText == null)
        {
            Debug.LogError("RehabGameUIController: Inspector의 UI 연결을 확인하세요.");
            enabled = false;
            return;
        }

        forceSlider.minValue = 0f;
        forceSlider.maxValue = 4095f;
        forceSlider.wholeNumbers = true;

        restButton.onClick.AddListener(RecordRest);
        maxButton.onClick.AddListener(RecordMax);
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
        if (calibrated && completed < 5)
        {
            float percent = CurrentPercent();

            if (waitingForRelease)
            {
                if (percent <= 20f)
                    waitingForRelease = false;
            }
            else if (percent >= 40f && percent <= 60f)
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

    private float CurrentPercent()
    {
        if (!calibrated)
            return 0f;

        // 개인별 휴식값을 0%, 최대 수행값을 100%로 환산한다.
        return Mathf.Clamp01(
            (forceSlider.value - restRaw) / (maxRaw - restRaw)
        ) * 100f;
    }

    private void RecordRest()
    {
        restRaw = forceSlider.value;
        hasRest = true;
        calibrated = false;
        invalidRange = false;
        maxCount = 0;
        heldTime = 0f;
        completed = 0;
        waitingForRelease = false;
        UpdateUI();
    }

    private void RecordMax()
    {
        if (!hasRest || maxCount >= 3)
            return;

        maxSamples[maxCount] = forceSlider.value;
        maxCount++;

        if (maxCount == 3)
        {
            // 세 번 측정한 최대 수행값 중 중앙값을 사용한다.
            Array.Sort(maxSamples);
            maxRaw = maxSamples[1];
            calibrated = maxRaw - restRaw >= 100f;
            invalidRange = !calibrated;
        }

        UpdateUI();
    }

    private void UpdateUI()
    {
        if (forceSlider == null || forceText == null ||
            repetitionText == null || messageText == null || maxButton == null)
            return;

        forceText.text = calibrated
            ? $"Force: {CurrentPercent():F1}%"
            : $"Raw: {forceSlider.value:F0}";

        repetitionText.text = $"Completed: {completed}/5";
        maxButton.interactable = hasRest && maxCount < 3;

        if (!hasRest)
            messageText.text = "Press Record Rest";
        else if (maxCount < 3)
            messageText.text = $"Press Record Max ({maxCount}/3)";
        else if (invalidRange)
            messageText.text = "Range too small. Record Rest again";
        else if (completed >= 5)
            messageText.text = "Session complete! Record Rest to restart";
        else if (waitingForRelease)
            messageText.text = "Good! Release below 20%";
        else
            messageText.text = $"Hold 40-60%: {heldTime:F1}/3.0 s";
    }
}