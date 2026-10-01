// Original stand-in for part of the Unity API, written for the ucl fixtures. Not Unity code. Apache-2.0.

namespace UnityEngine
{
    /// <summary>The legacy Input Manager.</summary>
    public static class Input
    {
        public static Vector3 mousePosition => throw null;

        public static bool anyKey => throw null;

        public static float GetAxis(string axisName) => throw null;

        public static float GetAxisRaw(string axisName) => throw null;

        public static bool GetButton(string buttonName) => throw null;

        public static bool GetButtonDown(string buttonName) => throw null;

        public static bool GetButtonUp(string buttonName) => throw null;

        public static bool GetKey(KeyCode key) => throw null;

        public static bool GetKeyDown(KeyCode key) => throw null;

        public static bool GetKeyUp(KeyCode key) => throw null;

        public static bool GetMouseButton(int button) => throw null;

        public static bool GetMouseButtonDown(int button) => throw null;
    }

    /// <summary>Key codes for Input.GetKey.</summary>
    public enum KeyCode
    {
        None = 0,
        Backspace = 8,
        Tab = 9,
        Return = 13,
        Escape = 27,
        Space = 32,
        Alpha0 = 48,
        Alpha1 = 49,
        Alpha2 = 50,
        A = 97,
        D = 100,
        E = 101,
        S = 115,
        W = 119,
        UpArrow = 273,
        DownArrow = 274,
        RightArrow = 275,
        LeftArrow = 276,
        LeftShift = 304,
        Mouse0 = 323,
    }
}
