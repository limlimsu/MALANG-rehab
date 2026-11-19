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
        private float[] channelRest;
        private float[] channelMax;

        public float Rest { get; private set; }
        public float Max { get; private set; }
        public bool HasRest { get; private set; }
        public bool HasMax { get; private set; }

        public bool HasChannelCalibration => channelRest != null && channelMax != null;
        public bool IsReady => HasChannelCalibration ||
                               (HasRest && HasMax && Mathf.Abs(Max - Rest) > MinSpan);

        /// <summary>각 손가락의 휴식값과 힘껏 누른 값을 따로 저장한다. 원시값 방향은 어느 쪽이든 가능하다.</summary>
        public bool SetChannels(float[] rest, float[] max, float minChannelSpan)
        {
            if (rest == null || max == null || rest.Length != 4 || max.Length != 4)
                return false;

            for (int i = 0; i < 4; i++)
            {
                if (float.IsNaN(rest[i]) || float.IsNaN(max[i]) ||
                    Mathf.Abs(max[i] - rest[i]) < minChannelSpan)
                    return false;
            }

            channelRest = (float[])rest.Clone();
            channelMax = (float[])max.Clone();
            return true;
        }

        /// <summary>네 손가락의 보정된 0~100%를 평균 내어 게임 입력으로 사용한다.</summary>
        public float NormalizeChannels(int[] raw)
        {
            if (!HasChannelCalibration || raw == null || raw.Length != 4)
                return 0f;

            float total = 0f;
            for (int i = 0; i < 4; i++)
                total += NormalizeChannel(i, raw[i]);
            return total * 25f;
        }

        public float NormalizeChannel(int index, float raw)
        {
            if (!HasChannelCalibration || index < 0 || index >= 4)
                return 0f;
            return Mathf.Clamp01((raw - channelRest[index]) / (channelMax[index] - channelRest[index]));
        }

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
            channelRest = null;
            channelMax = null;
            HasRest = false;
            HasMax = false;
            Rest = 0f;
            Max = 0f;
        }

        public float Normalize(float sum)
        {
            if (!HasRest || !HasMax || Mathf.Abs(Max - Rest) <= MinSpan)
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
