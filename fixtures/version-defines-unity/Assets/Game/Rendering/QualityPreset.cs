using UnityEngine;

namespace Game.Rendering
{
    public static class QualityPreset
    {
        public static string Describe()
        {
#if GAME_UNITY_6_3_APIS
            return "Unity 6.3 rendering path";
#elif GAME_UNITY_6_0_LTS
            return "Unity 6.0 LTS rendering path";
#else
            return "fallback rendering path";
#endif
        }

        public static void Log() => Debug.Log(Describe());
    }
}
