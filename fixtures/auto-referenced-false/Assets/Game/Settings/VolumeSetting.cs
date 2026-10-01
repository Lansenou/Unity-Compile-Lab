using Game.Audio;

namespace Game.Settings
{
    public static class VolumeSetting
    {
        public static void Apply(float value) => Mixer.MasterVolume = value;
    }
}
