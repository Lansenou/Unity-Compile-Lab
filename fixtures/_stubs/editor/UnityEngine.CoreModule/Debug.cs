// Original stand-in for part of the Unity API, written for the ucl fixtures. Not Unity code. Apache-2.0.

namespace UnityEngine
{
    /// <summary>The type of a log message.</summary>
    public enum LogType
    {
        Error = 0,
        Assert = 1,
        Warning = 2,
        Log = 3,
        Exception = 4,
    }

    /// <summary>Logging to the Console window.</summary>
    public static class Debug
    {
        public static void Log(object message) => throw Native.Unavailable();

        public static void Log(object message, Object context) => throw Native.Unavailable();

        public static void LogFormat(string format, params object[] args) => throw Native.Unavailable();

        public static void LogWarning(object message) => throw Native.Unavailable();

        public static void LogWarning(object message, Object context) => throw Native.Unavailable();

        public static void LogError(object message) => throw Native.Unavailable();

        public static void LogError(object message, Object context) => throw Native.Unavailable();

        public static void LogException(System.Exception exception) => throw Native.Unavailable();

        public static void Assert(bool condition) => throw Native.Unavailable();

        public static void Assert(bool condition, string message) => throw Native.Unavailable();

        public static void DrawLine(Vector3 start, Vector3 end) => throw Native.Unavailable();
    }
}
