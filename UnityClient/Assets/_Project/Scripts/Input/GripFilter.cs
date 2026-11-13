namespace Malang.Input
{
    /// <summary>채널 합에 거는 지수 이동 평균(EMA): y = α·x + (1-α)·y</summary>
    public class GripFilter
    {
        private bool hasValue;
        private float value;

        public float Value => value;

        public void Reset()
        {
            hasValue = false;
            value = 0f;
        }

        public float Step(float x, float alpha)
        {
            if (!hasValue)
            {
                value = x;
                hasValue = true;
            }
            else
            {
                value = alpha * x + (1f - alpha) * value;
            }

            return value;
        }
    }
}
