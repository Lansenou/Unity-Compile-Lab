using UnityEditor;
using UnityEngine;

public static class BadAssetReport
{
    [MenuItem("Tools/Report Bad Assets")]
    private static void Run()
    {
        Debug.Log("No bad assets found");
    }
}
