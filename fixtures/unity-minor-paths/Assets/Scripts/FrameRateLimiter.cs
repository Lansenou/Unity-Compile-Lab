using UnityEngine;

public class FrameRateLimiter : MonoBehaviour
{
    [SerializeField] private int batterySaverRate = 30;

    private void Start()
    {
#if UNITY_6000_3_OR_NEWER
        // New branch written for Unity 6.3 but never compiled on 6.0: it has a typo.
        var rate = batterySaverRate;
        Application.targetFrameRate = rat;
#else
        Application.targetFrameRate = batterySaverRate;
#endif
    }
}
