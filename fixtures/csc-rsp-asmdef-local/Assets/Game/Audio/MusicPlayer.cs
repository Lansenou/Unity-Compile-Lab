using UnityEngine;

namespace Game.Audio
{
    public class MusicPlayer : MonoBehaviour
    {
        public void PlayTrack(string track)
        {
#if AUDIO_VERBOSE_LOGGING
            Debug.Log($"Playing {track}");
#endif
        }
    }
}
