using System;
using UnityEditor;

// An Editor script compiled with the Editor assemblies' API compatibility level.
public static class ScoreBufferMenu
{
    [MenuItem("Tools/Score buffer size")]
    public static void Report()
    {
        Span<int> view = new int[4].AsSpan();
        UnityEngine.Debug.Log("Score buffer: " + view.Length);
    }
}
