namespace Malang.UI
{
    /// <summary>자주 바뀌는 숫자 표시(타이머 등)를 문자열 할당 없이 쓰기 위한 캐시.</summary>
    public static class IntStrings
    {
        private const int CacheSize = 201;
        private static string[] cache;

        public static string Get(int value)
        {
            if (cache == null)
            {
                cache = new string[CacheSize];
                for (int i = 0; i < CacheSize; i++)
                    cache[i] = i.ToString();
            }

            return value >= 0 && value < CacheSize ? cache[value] : value.ToString();
        }
    }
}
