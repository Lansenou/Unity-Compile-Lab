// Original stand-in for part of the Unity API, written for the ucl fixtures. Not Unity code. Apache-2.0.

namespace UnityEngine
{
    /// <summary>Base class of every object Unity can reference.</summary>
    public class Object
    {
        public string name { get => throw Native.Unavailable(); set => throw Native.Unavailable(); }

        public HideFlags hideFlags { get => throw Native.Unavailable(); set => throw Native.Unavailable(); }

        public int GetInstanceID() => throw Native.Unavailable();

        public static T Instantiate<T>(T original) where T : Object => throw Native.Unavailable();

        public static T Instantiate<T>(T original, Vector3 position, Quaternion rotation) where T : Object => throw Native.Unavailable();

        public static void Destroy(Object obj) => throw Native.Unavailable();

        public static void Destroy(Object obj, float t) => throw Native.Unavailable();

        public static void DontDestroyOnLoad(Object target) => throw Native.Unavailable();

        public static T FindAnyObjectByType<T>() where T : Object => throw Native.Unavailable();

        public static bool operator ==(Object x, Object y) => throw Native.Unavailable();

        public static bool operator !=(Object x, Object y) => throw Native.Unavailable();

        public static implicit operator bool(Object exists) => throw Native.Unavailable();

        public override bool Equals(object other) => throw Native.Unavailable();

        public override int GetHashCode() => throw Native.Unavailable();

        public override string ToString() => throw Native.Unavailable();
    }

    /// <summary>Bit mask that controls object destruction, saving and visibility in inspectors.</summary>
    [System.Flags]
    public enum HideFlags
    {
        None = 0,
        HideInHierarchy = 1,
        HideInInspector = 2,
        DontSaveInEditor = 4,
        NotEditable = 8,
        DontSaveInBuild = 16,
        DontUnloadUnusedAsset = 32,
        DontSave = 52,
        HideAndDontSave = 61,
    }
}
