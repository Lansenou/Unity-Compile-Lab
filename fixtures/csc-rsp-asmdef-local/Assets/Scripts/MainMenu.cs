using Game.Audio;
using UnityEngine;

public class MainMenu : MonoBehaviour
{
    [SerializeField] private MusicPlayer music;

    private void Start()
    {
#if GAME_GLOBAL_RSP
        music.PlayTrack("menu_theme");
#endif
    }
}
