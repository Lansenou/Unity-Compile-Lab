using UnityEngine;

public class PathfindingService : MonoBehaviour
{
    private void Start()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        Debug.Log("WebGL has no threads; pathfinding runs on the main thread");
#else
        new Game.Threading.BackgroundWorker().Start(() => Debug.Log("Baking navigation grid"));
#endif
    }
}
