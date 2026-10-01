using UnityEngine;

public class GameBootstrap : MonoBehaviour
{
    private void Start()
    {
#if (UNITY_EDITOR || DEVELOPMENT_BUILD) && !DISABLE_DEBUG_TOOLS
        gameObject.AddComponent<Game.DebugTools.CheatConsole>();
#endif
        Debug.Log("Game started");
    }
}
