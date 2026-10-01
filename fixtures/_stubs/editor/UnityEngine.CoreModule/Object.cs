// Original stand-in for part of the Unity API, written for the ucl fixtures. Not Unity code. Apache-2.0.

namespace UnityEngine
{
    /// <summary>Base class of every object Unity can reference.</summary>
    public class Object
    {
        public string name { get => throw null; set => throw null; }

        public HideFlags hideFlags { get => throw null; set => throw null; }

        public int GetInstanceID() => throw null;

        public static T Instantiate<T>(T original) where T : Object => throw null;

        public static T Instantiate<T>(T original, Vector3 position, Quaternion rotation) where T : Object => throw null;

        public static void Destroy(Object obj) => throw null;

        public static void Destroy(Object obj, float t) => throw null;

        public static void DontDestroyOnLoad(Object target) => throw null;

        public static T FindAnyObjectByType<T>() where T : Object => throw null;

        public static bool operator ==(Object x, Object y) => throw null;

        public static bool operator !=(Object x, Object y) => throw null;

        public static implicit operator bool(Object exists) => throw null;

        public override bool Equals(object other) => throw null;

        public override int GetHashCode() => throw null;

        public override string ToString() => throw null;
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
