// Original stand-in for part of the Unity API, written for the ucl fixtures. Not Unity code. Apache-2.0.

namespace UnityEngine
{
    /// <summary>Access to application runtime data.</summary>
    public static class Application
    {
        public static bool isPlaying => throw null;

        public static bool isEditor => throw null;

        public static RuntimePlatform platform => throw null;

        public static string version => throw null;

        public static string unityVersion => throw null;

        public static string productName => throw null;

        public static string dataPath => throw null;

        public static string persistentDataPath => throw null;

        public static int targetFrameRate { get => throw null; set => throw null; }

        public static void Quit() => throw null;

        public static void OpenURL(string url) => throw null;
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
        public static float time => throw null;

        public static float deltaTime => throw null;

        public static float unscaledDeltaTime => throw null;

        public static float fixedDeltaTime { get => throw null; set => throw null; }

        public static float timeScale { get => throw null; set => throw null; }

        public static int frameCount => throw null;

        public static float realtimeSinceStartup => throw null;
    }
}
