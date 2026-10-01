using UnityEngine;

public class GameManager : MonoBehaviour
{
    private void Start()
    {
        AnalyticsClient.Track("game_manager_ready");
    }
}
