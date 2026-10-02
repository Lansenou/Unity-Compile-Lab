// Original stand-in for part of the Unity API, written for the ucl fixtures. Not Unity code. Apache-2.0.

namespace UnityEngine
{
    /// <summary>Content of a GUI element: text, image and tooltip.</summary>
    public class GUIContent
    {
        public GUIContent() => throw Native.Unavailable();

        public GUIContent(string text) => throw Native.Unavailable();

        public GUIContent(string text, string tooltip) => throw Native.Unavailable();

        public string text { get => throw Native.Unavailable(); set => throw Native.Unavailable(); }

        public string tooltip { get => throw Native.Unavailable(); set => throw Native.Unavailable(); }
    }

    /// <summary>Immediate-mode GUI layout.</summary>
    public static class GUILayout
    {
        public static bool Button(string text) => throw Native.Unavailable();

        public static void Label(string text) => throw Native.Unavailable();

        public static void Space(float pixels) => throw Native.Unavailable();

        public static void BeginHorizontal() => throw Native.Unavailable();

        public static void EndHorizontal() => throw Native.Unavailable();

        public static void BeginVertical() => throw Native.Unavailable();

        public static void EndVertical() => throw Native.Unavailable();
    }
}
