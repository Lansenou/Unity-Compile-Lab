using UnityEngine;

public class InputBridge : MonoBehaviour
{
#if ENABLE_INPUT_SYSTEM && ENABLE_LEGACY_INPUT_MANAGER
    private const string Mode = "both";
#elif ENABLE_INPUT_SYSTEM
    private const string Mode = "input system";
#elif ENABLE_LEGACY_INPUT_MANAGER
    private const string Mode = "legacy";
#endif

    private void Start()
    {
        Debug.Log("Active input handling: " + Mode);
#if ENABLE_LEGACY_INPUT_MANAGER
        Debug.Log(Input.GetKey(KeyCode.Space));
#endif
    }
}
