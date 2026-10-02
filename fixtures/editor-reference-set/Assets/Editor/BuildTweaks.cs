// Fixture source for the ucl conformance corpus. Original code, Apache-2.0.

public static class BuildTweaks
{
    // UnityEditor.Graphs.dll is in Managed/, beside Managed/UnityEngine/.
    public static System.Type GraphType => typeof(UnityEditor.Graphs.Graph);

    // Every installed platform's UnityEditor.*.Extensions.dll is an Editor reference, whatever the active platform.
    public static void Apply()
    {
        UnityEditor.WebGL.UserBuildSettings.codeOptimization = UnityEditor.WebGL.WasmCodeOptimization.DiskSize;
        UnityEditor.WindowsStandalone.UserBuildSettings.createSolution = false;
    }
}
