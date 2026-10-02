// Original stand-in for part of the Unity API, written for the ucl fixtures. Not Unity code. Apache-2.0.

namespace UnityEditor.Graphs
{
    /// <summary>A node graph.</summary>
    public class Graph : UnityEngine.ScriptableObject
    {
        public void Clear() => throw null;
    }

    /// <summary>A node of a <see cref="Graph"/>.</summary>
    public class Node : UnityEngine.ScriptableObject
    {
        public string title { get => throw null; set => throw null; }
    }
}
