using UnityEngine;

public class HapticButton : MonoBehaviour
{
    [SerializeField] private int pulseMilliseconds = 40;

    public void OnPressed()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        Game.AndroidBridge.AndroidHaptics.Vibrate(pulseMilliseconds);
#else
        Debug.Log($"Haptic pulse of {pulseMilliseconds} ms skipped on this platform");
#endif
    }
}
