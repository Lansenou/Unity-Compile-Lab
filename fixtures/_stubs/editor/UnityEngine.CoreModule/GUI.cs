// Original stand-in for part of the Unity API, written for the ucl fixtures. Not Unity code. Apache-2.0.

namespace UnityEngine
{
    /// <summary>Content of a GUI element: text, image and tooltip.</summary>
    public class GUIContent
    {
        public GUIContent() => throw null;

        public GUIContent(string text) => throw null;

        public GUIContent(string text, string tooltip) => throw null;

        public string text { get => throw null; set => throw null; }

        public string tooltip { get => throw null; set => throw null; }
    }

    /// <summary>Immediate-mode GUI layout.</summary>
    public static class GUILayout
    {
        public static bool Button(string text) => throw null;

        public static void Label(string text) => throw null;

        public static void Space(float pixels) => throw null;

        public static void BeginHorizontal() => throw null;

        public static void EndHorizontal() => throw null;

        public static void BeginVertical() => throw null;

        public static void EndVertical() => throw null;
    }
}
