using Game.Audio;
using UnityEngine;

public class MuteButton : MonoBehaviour
{
    public void OnClick()
    {
        Mixer.MasterVolume = 0f;
    }
}
