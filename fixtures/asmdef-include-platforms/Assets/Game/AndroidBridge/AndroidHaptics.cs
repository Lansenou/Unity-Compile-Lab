using UnityEngine;

namespace Game.AndroidBridge
{
    public static class AndroidHaptics
    {
        public static void Vibrate(int milliseconds)
        {
            Debug.Log($"Vibrating for {milliseconds} ms");
        }
    }
}
