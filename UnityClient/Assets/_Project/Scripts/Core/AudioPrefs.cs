using UnityEngine;

namespace Malang.Core
{
    /// <summary>소리 켜기/끄기 (보호자 모드). M4 에서 profile.json 으로 옮긴다.</summary>
    public static class AudioPrefs
    {
        private const string PrefKey = "malang.sound";
        private static int cache = -1;

        public static bool SoundEnabled
        {
            get
            {
                if (cache < 0)
                    cache = PlayerPrefs.GetInt(PrefKey, 1);
                return cache == 1;
            }
            set
            {
                cache = value ? 1 : 0;
                PlayerPrefs.SetInt(PrefKey, cache);
                PlayerPrefs.Save();
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            cache = -1;
        }
    }
}
