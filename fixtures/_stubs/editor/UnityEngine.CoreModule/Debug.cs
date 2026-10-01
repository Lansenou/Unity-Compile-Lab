// Original stand-in for part of the Unity API, written for the ucl fixtures. Not Unity code. Apache-2.0.

namespace UnityEngine
{
    /// <summary>Logging to the Console window.</summary>
    public static class Debug
    {
        public static void Log(object message) => throw null;

        public static void Log(object message, Object context) => throw null;

        public static void LogFormat(string format, params object[] args) => throw null;

        public static void LogWarning(object message) => throw null;

        public static void LogWarning(object message, Object context) => throw null;

        public static void LogError(object message) => throw null;

        public static void LogError(object message, Object context) => throw null;

        public static void LogException(System.Exception exception) => throw null;

        public static void Assert(bool condition) => throw null;

        public static void Assert(bool condition, string message) => throw null;

        public static void DrawLine(Vector3 start, Vector3 end) => throw null;
    }
}
