using System;

namespace Malang.Training
{
    public enum GripTransition
    {
        None,
        Gripped,
        Released
    }

    /// <summary>
    /// 쥠·놓음 이중 임계값 판정 (SPEC §7).
    ///   Released → Gripped : g ≥ gripThreshold
    ///   Gripped  → Released: g ≤ releaseThreshold
    ///   그 사이에서는 직전 상태 유지.
    /// FSR 은 손가락 펴짐을 모르므로 "놓음 = 힘이 임계 이하로 떨어짐" 으로만 판단한다.
    /// </summary>
    public class HysteresisDetector
    {
        public float GripThreshold { get; private set; }
        public float ReleaseThreshold { get; private set; }
        public bool IsGripped { get; private set; }

        public event Action<double> OnGrip;
        public event Action<double> OnRelease;

        public HysteresisDetector(float gripThreshold, float releaseThreshold)
        {
            Configure(gripThreshold, releaseThreshold);
        }

        public void Configure(float gripThreshold, float releaseThreshold)
        {
            GripThreshold = gripThreshold;
            ReleaseThreshold = releaseThreshold;
        }

        public void Reset(bool gripped = false)
        {
            IsGripped = gripped;
        }

        /// <summary>이번 값으로 상태를 갱신하고, 바뀌었으면 그 전환을 돌려준다.</summary>
        public GripTransition Update(float g, double t)
        {
            if (!IsGripped && g >= GripThreshold)
            {
                IsGripped = true;
                OnGrip?.Invoke(t);
                return GripTransition.Gripped;
            }

            if (IsGripped && g <= ReleaseThreshold)
            {
                IsGripped = false;
                OnRelease?.Invoke(t);
                return GripTransition.Released;
            }

            return GripTransition.None;
        }
    }
}
