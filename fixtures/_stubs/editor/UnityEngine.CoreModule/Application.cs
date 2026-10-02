// Original stand-in for part of the Unity API, written for the ucl fixtures. Not Unity code. Apache-2.0.

namespace UnityEngine
{
    /// <summary>Access to application runtime data.</summary>
    public static class Application
    {
        public static bool isPlaying => throw Native.Unavailable();

        public static bool isEditor => throw Native.Unavailable();

        public static RuntimePlatform platform => throw Native.Unavailable();

        public static string version => throw Native.Unavailable();

        public static string unityVersion => throw Native.Unavailable();

        public static string productName => throw Native.Unavailable();

        public static string dataPath => throw Native.Unavailable();

        public static string persistentDataPath => throw Native.Unavailable();

        public static int targetFrameRate { get => throw Native.Unavailable(); set => throw Native.Unavailable(); }

        public static void Quit() => throw Native.Unavailable();

        public static void OpenURL(string url) => throw Native.Unavailable();
    }

    /// <summary>The platform the application is running on.</summary>
    public enum RuntimePlatform
    {
        OSXEditor = 0,
        OSXPlayer = 1,
        WindowsPlayer = 2,
        WindowsEditor = 7,
        IPhonePlayer = 8,
        Android = 11,
        LinuxPlayer = 13,
        LinuxEditor = 16,
        WebGLPlayer = 17,
    }

    /// <summary>Time information.</summary>
    public static class Time
    {
        public static float time => throw Native.Unavailable();

        public static float deltaTime => throw Native.Unavailable();

        public static float unscaledDeltaTime => throw Native.Unavailable();

        public static float fixedDeltaTime { get => throw Native.Unavailable(); set => throw Native.Unavailable(); }

        public static float timeScale { get => throw Native.Unavailable(); set => throw Native.Unavailable(); }

        public static int frameCount => throw Native.Unavailable();

        public static float realtimeSinceStartup => throw Native.Unavailable();
    }
}
