using System;
using System.Collections.Generic;
using Malang.Core;
using UnityEngine;

namespace Malang.Input
{
    /// <summary>
    /// 개인 보정: 휴식값 = 0%, 최대값 = 100%.
    /// g = clamp((sum - rest) / (max - rest) × 100, 0, 100)
    /// 눌렀을 때 원시값이 줄어드는 배선이어도(max &lt; rest) 같은 식으로 동작한다.
    /// M2: 계산과 값 저장만. 측정 흐름(휴식 2초, 최대 3회)은 M5 의 보정 화면에서.
    /// </summary>
    public class Calibrator
    {
        private const float MinSpan = 0.0001f;

        public float Rest { get; private set; }
        public float Max { get; private set; }
        public bool HasRest { get; private set; }
        public bool HasMax { get; private set; }

        public bool IsReady => HasRest && HasMax && Mathf.Abs(Max - Rest) > MinSpan;

        public void SetRest(float rest)
        {
            Rest = rest;
            HasRest = true;
        }

        public void SetMax(float max)
        {
            Max = max;
            HasMax = true;
        }

        public void Clear()
        {
            HasRest = false;
            HasMax = false;
            Rest = 0f;
            Max = 0f;
        }

        public float Normalize(float sum)
        {
            if (!IsReady)
                return 0f;

            return Mathf.Clamp((sum - Rest) / (Max - Rest) * 100f, 0f, 100f);
        }

        /// <summary>최대 측정 피크들의 대푯값 (GameConfig.calMaxAggregate).</summary>
        public static float Aggregate(IList<float> peaks, MaxAggregate mode)
        {
            if (peaks == null || peaks.Count == 0)
                throw new ArgumentException("peaks 가 비어 있습니다.");

            var sorted = new List<float>(peaks);
            sorted.Sort();

            if (mode == MaxAggregate.Max)
                return sorted[sorted.Count - 1];

            int mid = sorted.Count / 2;
            return sorted.Count % 2 == 1 ? sorted[mid] : (sorted[mid - 1] + sorted[mid]) * 0.5f;
        }
    }
}
