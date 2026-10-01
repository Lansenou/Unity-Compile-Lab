using UnityEngine;

public class DebugOverlay : MonoBehaviour
{
#if DEVELOPMENT_BUILD || UNITY_EDITOR
    private void OnGUI()
    {
        GUILayout.Label("FPS: " + (1f / Time.deltaTime).ToString("0"));
    }
#endif
}
