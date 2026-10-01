using UnityEngine;

public class BackendInfo : MonoBehaviour
{
    private void Start()
    {
#if ENABLE_IL2CPP
        Debug.Log("IL2CPP");
#elif ENABLE_MONO
        Debug.Log("Mono");
#endif
    }
}
