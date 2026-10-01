using UnityEngine;

public class AnalyticsSceneHook : MonoBehaviour
{
    private void Start()
    {
        var manager = FindAnyObjectByType<GameManager>();
        AnalyticsClient.Track(manager != null ? "session_start" : "session_start_no_manager");
    }
}
