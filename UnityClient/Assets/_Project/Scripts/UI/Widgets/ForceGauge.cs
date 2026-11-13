using Malang.Input;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Malang.UI
{
    /// <summary>지금 아이가 맞춰야 할 게이지 위치.</summary>
    public enum GaugeTarget
    {
        None,
        GripZone,   // 1단계: 쥐기 구간(쥠 임계 이상)
        RelaxZone,  // 1단계: 쉬기 구간(놓음 임계 이하)
        Band        // 2단계: 목표 구간
    }

    /// <summary>
    /// 세로 힘 게이지 (SPEC §8.0 + 가독성 보강).
    /// - 채움 높이 = g%, 아래 숫자 NN%
    /// - 2단계: 초록 목표 구간. 채움 색은 구간 아래 보라 / 안 초록 / 위 분홍. 구간 안에 있으면 띠가 밝아진다.
    /// - 1단계: 쥐기 구간(연한 노랑)·쉬기 구간(연한 하늘)을 면으로 칠하고 경계에 점선.
    ///   채움 색은 쥐기 구간 도달 시 노랑, 쉬기 구간 도달 시 하늘.
    /// - 목표 말풍선: 게이지 왼쪽에서 지금 목표 위치를 가리키며 깜빡인다(글자 + 색, 색각 고려).
    /// - 목표 위치에 들어가는 순간 그 구간이 한 번 번쩍인다.
    /// </summary>
    public class ForceGauge : MonoBehaviour
    {
        private const float Percent = 100f;
        private const int StateBelow = 0;
        private const int StateInside = 1;
        private const int StateAbove = 2;
        private const int StateZoneHigh = 3;
        private const int StateZoneLow = 4;
        private const int StateMiddle = 5;

        private const float FlashSeconds = 0.35f;
        private const float FlashBoost = 0.45f;
        private const float BandGlowAlpha = 0.45f;
        private const float PulseHz = 1.5f;
        private const float PulseAmount = 0.06f;

        private static string[] labels;

        [SerializeField] private RectTransform fill;
        [SerializeField] private Image fillImage;
        [SerializeField] private RectTransform band;
        [SerializeField] private Image bandGlow;
        [SerializeField] private Image zoneHigh;
        [SerializeField] private Image zoneLow;
        [SerializeField] private RectTransform[] markers = new RectTransform[0];
        [SerializeField] private TMP_Text valueText;
        [SerializeField] private RectTransform callout;
        [SerializeField] private Image calloutBackground;
        [SerializeField] private Image calloutTail;
        [SerializeField] private LocText calloutLabel;

        private bool hasBand;
        private float bandLow;
        private float bandHigh;
        private bool hasZones;
        private float gripAt;
        private float releaseAt;
        private GaugeTarget target = GaugeTarget.None;
        private bool wasInTarget;
        private float flashTimer;
        private Image flashImage;
        private float zoneHighAlpha;
        private float zoneLowAlpha;
        private float bandGlowBaseAlpha;
        private int lastShown = -1;
        private int lastState = -1;

        private void Awake()
        {
            zoneHighAlpha = zoneHigh.color.a;
            zoneLowAlpha = zoneLow.color.a;
            bandGlowBaseAlpha = bandGlow.color.a;
        }

        // ---------- 설정 ----------

        /// <summary>2단계 목표 구간.</summary>
        public void SetBand(float low, float high)
        {
            hasBand = true;
            bandLow = Mathf.Clamp(Mathf.Min(low, high), 0f, Percent);
            bandHigh = Mathf.Clamp(Mathf.Max(low, high), 0f, Percent);

            band.gameObject.SetActive(true);
            SetVerticalRange(band, bandLow, bandHigh);
            lastState = -1;
        }

        public void ClearBand()
        {
            hasBand = false;
            band.gameObject.SetActive(false);
            lastState = -1;
        }

        /// <summary>1단계 쥐기 구간(gripAt~100)과 쉬기 구간(0~releaseAt), 경계 점선.</summary>
        public void SetZones(float grip, float release)
        {
            hasZones = true;
            gripAt = Mathf.Clamp(grip, 0f, Percent);
            releaseAt = Mathf.Clamp(release, 0f, Percent);

            zoneHigh.gameObject.SetActive(true);
            zoneLow.gameObject.SetActive(true);
            SetVerticalRange(zoneHigh.rectTransform, gripAt, Percent);
            SetVerticalRange(zoneLow.rectTransform, 0f, releaseAt);
            SetMarkers(gripAt, releaseAt);
            lastState = -1;
        }

        public void ClearZones()
        {
            hasZones = false;
            zoneHigh.gameObject.SetActive(false);
            zoneLow.gameObject.SetActive(false);
            ClearMarkers();
            lastState = -1;
        }

        /// <summary>점선 위치(%)를 지정한다. 값 개수만큼 앞에서부터 켜고 나머지는 끈다.</summary>
        public void SetMarkers(params float[] values)
        {
            for (int i = 0; i < markers.Length; i++)
            {
                bool on = values != null && i < values.Length;
                markers[i].gameObject.SetActive(on);
                if (!on)
                    continue;

                float y = Mathf.Clamp(values[i], 0f, Percent) / Percent;
                markers[i].anchorMin = new Vector2(0f, y);
                markers[i].anchorMax = new Vector2(1f, y);
                markers[i].anchoredPosition = Vector2.zero;
            }
        }

        public void ClearMarkers()
        {
            SetMarkers(null);
        }

        /// <summary>지금 목표. 바뀔 때만 말풍선을 옮긴다.</summary>
        public void SetTarget(GaugeTarget newTarget)
        {
            if (newTarget == target)
                return;

            target = newTarget;
            wasInTarget = true; // 이미 목표 안에 있을 때 바로 번쩍이지 않도록
            ApplyCallout();
        }

        private void ApplyCallout()
        {
            float y;
            string key;
            Color color;

            switch (target)
            {
                case GaugeTarget.GripZone:
                    y = (gripAt + Percent) * 0.5f;
                    key = "gauge.grip";
                    color = Palette.Yellow;
                    break;
                case GaugeTarget.RelaxZone:
                    y = releaseAt * 0.5f;
                    key = "gauge.relax";
                    color = Palette.RelaxBlue;
                    break;
                case GaugeTarget.Band:
                    y = (bandLow + bandHigh) * 0.5f;
                    key = "gauge.here";
                    color = Palette.Green;
                    break;
                default:
                    callout.gameObject.SetActive(false);
                    return;
            }

            callout.gameObject.SetActive(true);
            callout.anchorMin = new Vector2(1f, y / Percent);
            callout.anchorMax = new Vector2(1f, y / Percent);
            callout.anchoredPosition = Vector2.zero;
            calloutBackground.color = color;
            calloutTail.color = color;
            calloutLabel.Key = key;
        }

        // ---------- 매 프레임 ----------

        private void OnEnable()
        {
            lastShown = -1;
            lastState = -1;
            flashTimer = 0f;
            RestoreFlashImage();
        }

        private void Update()
        {
            IGripInput input = GripSource.Input;
            Show(input != null ? input.Grip : 0f);
            Animate(Time.unscaledDeltaTime);
        }

        public void Show(float g)
        {
            g = Mathf.Clamp(g, 0f, Percent);

            Vector2 max = fill.anchorMax;
            max.y = g / Percent;
            fill.anchorMax = max;

            int state = StateFor(g);
            if (state != lastState)
            {
                lastState = state;
                fillImage.color = ColorFor(state);

                if (hasBand)
                {
                    Color glow = bandGlow.color;
                    glow.a = state == StateInside ? BandGlowAlpha : bandGlowBaseAlpha;
                    bandGlow.color = glow;
                }
            }

            bool inTarget = IsInTarget(g);
            if (inTarget && !wasInTarget)
                StartFlash();
            wasInTarget = inTarget;

            int shown = Mathf.RoundToInt(g);
            if (shown != lastShown)
            {
                lastShown = shown;
                valueText.text = Label(shown);
            }
        }

        private int StateFor(float g)
        {
            if (hasBand)
                return g < bandLow ? StateBelow : (g <= bandHigh ? StateInside : StateAbove);
            if (hasZones)
                return g >= gripAt ? StateZoneHigh : (g <= releaseAt ? StateZoneLow : StateMiddle);
            return StateBelow;
        }

        private static Color ColorFor(int state)
        {
            switch (state)
            {
                case StateInside: return Palette.Green;
                case StateAbove: return Palette.PinkDeep;
                case StateZoneHigh: return Palette.Yellow;
                case StateZoneLow: return Palette.RelaxBlue;
                default: return Palette.Purple;
            }
        }

        private bool IsInTarget(float g)
        {
            switch (target)
            {
                case GaugeTarget.GripZone: return g >= gripAt;
                case GaugeTarget.RelaxZone: return g <= releaseAt;
                case GaugeTarget.Band: return hasBand && g >= bandLow && g <= bandHigh;
                default: return false;
            }
        }

        private void StartFlash()
        {
            RestoreFlashImage();
            flashImage = target == GaugeTarget.GripZone ? zoneHigh
                : target == GaugeTarget.RelaxZone ? zoneLow
                : target == GaugeTarget.Band ? bandGlow
                : null;
            flashTimer = flashImage != null ? FlashSeconds : 0f;
        }

        private void Animate(float dt)
        {
            if (callout.gameObject.activeSelf)
            {
                float pulse = 1f + PulseAmount * Mathf.Sin(Time.unscaledTime * PulseHz * 2f * Mathf.PI);
                callout.localScale = new Vector3(pulse, pulse, 1f);
            }

            if (flashTimer <= 0f || flashImage == null)
                return;

            flashTimer -= dt;
            float k = Mathf.Clamp01(flashTimer / FlashSeconds);
            Color c = flashImage.color;
            c.a = Mathf.Clamp01(BaseAlpha(flashImage) + FlashBoost * k);
            flashImage.color = c;

            if (flashTimer <= 0f)
                RestoreFlashImage();
        }

        private float BaseAlpha(Image image)
        {
            if (image == zoneHigh)
                return zoneHighAlpha;
            if (image == zoneLow)
                return zoneLowAlpha;
            return lastState == StateInside ? BandGlowAlpha : bandGlowBaseAlpha;
        }

        private void RestoreFlashImage()
        {
            if (flashImage == null)
                return;

            Color c = flashImage.color;
            c.a = BaseAlpha(flashImage);
            flashImage.color = c;
            flashImage = null;
        }

        private static void SetVerticalRange(RectTransform rt, float low, float high)
        {
            rt.anchorMin = new Vector2(0f, low / Percent);
            rt.anchorMax = new Vector2(1f, high / Percent);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private static string Label(int value)
        {
            if (labels == null)
            {
                labels = new string[(int)Percent + 1];
                for (int i = 0; i < labels.Length; i++)
                    labels[i] = i + "%";
            }

            return labels[Mathf.Clamp(value, 0, labels.Length - 1)];
        }
    }
}
