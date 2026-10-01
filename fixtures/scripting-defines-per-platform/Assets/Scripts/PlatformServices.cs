using UnityEngine;

public class PlatformServices : MonoBehaviour
{
    private void Start()
    {
#if STEAMWORKS_NET
        Debug.Log("Signing in to Steam");
#elif GOOGLE_PLAY_GAMES
        Debug.Log("Signing in to Google Play Games");
#endif
    }

    public string PromptStyle()
    {
#if USE_KEYBOARD_PROMPTS
        return "keyboard";
#elif USE_TOUCH_PROMPTS
        return "touch";
#else
#error No prompt style is defined for this build target group
#endif
    }
}
