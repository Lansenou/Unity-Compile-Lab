using System;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class ProjectHostBuild
{
    public static void Build()
    {
        var args = Environment.GetCommandLineArgs();
        var i = Array.IndexOf(args, "-playerOutput");
        if (i < 0 || i + 1 >= args.Length) throw new ArgumentException("Missing -playerOutput");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        const string scene = "Assets/UclProjectHost/Host.unity";
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), scene);
        PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
        PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.Standalone, ManagedStrippingLevel.Disabled);
        PlayerSettings.runInBackground = true;
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = new[] { scene },
            locationPathName = args[i + 1],
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.Development | BuildOptions.IncludeTestAssemblies
        });
        Debug.Log("PROJECT_HOST_BUILD_SECONDS " + report.summary.totalTime.TotalSeconds);
        if (report.summary.result != BuildResult.Succeeded)
            throw new Exception("Player build failed: " + report.summary.result);
    }
}
