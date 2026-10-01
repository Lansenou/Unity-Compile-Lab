// Injected by oracle/record.sh and oracle/record.ps1 (player cells only). Run with
//   Unity -batchmode -nographics -quit -projectPath <copy> -buildTarget <name> -logFile <log>
//         -executeMethod Ucl.Oracle.CompilePlayer -uclOutDir <dir> [-uclDevelopment]
// Unity can call this only after the editor scripts compiled; otherwise the recorder reports the cell as
// "editor-compile-failed". Every line the recorder reads starts with "UCL-ORACLE-".
#pragma warning disable CS0618 // keep working if an API used here becomes obsolete in a later Unity 6 minor

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Player;
using UnityEngine;

namespace Ucl
{
    public static class Oracle
    {
        public static void CompilePlayer()
        {
            var exitCode = 0;
            try
            {
                var args = Environment.GetCommandLineArgs();
                var outDir = Arg(args, "-uclOutDir") ?? Path.Combine(Path.GetTempPath(), "ucl-oracle-player");
                var development = args.Contains("-uclDevelopment");
                var target = EditorUserBuildSettings.activeBuildTarget;
                var settings = new ScriptCompilationSettings
                {
                    target = target,
                    group = BuildPipeline.GetBuildTargetGroup(target),
                    options = development ? ScriptCompilationOptions.DevelopmentBuild : ScriptCompilationOptions.None,
                };
                Directory.CreateDirectory(outDir);
                var started = DateTime.UtcNow.AddSeconds(-2);
                Log($"UCL-ORACLE-BEGIN target={settings.target} group={settings.group} options={settings.options}");
                var result = PlayerBuildInterface.CompilePlayerScripts(settings, outDir);
                var names = new SortedSet<string>(StringComparer.Ordinal);
                IEnumerable<string> compiled = result.assemblies;
                foreach (var a in compiled ?? Enumerable.Empty<string>())
                {
                    var n = Path.GetFileName(a);
                    names.Add(n.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) ? n.Substring(0, n.Length - 4) : n);
                }

                foreach (var n in names)
                {
                    Log("UCL-ORACLE-ASSEMBLY: " + n);
                }

                // Compiler response files written by this player compilation (defines per assembly).
                var bee = Path.Combine("Library", "Bee", "artifacts");
                if (Directory.Exists(bee))
                {
                    foreach (var rsp in Directory.GetFiles(bee, "*.rsp", SearchOption.AllDirectories)
                                 .Where(f => File.GetLastWriteTimeUtc(f) >= started)
                                 .OrderBy(f => f, StringComparer.Ordinal))
                    {
                        Log("UCL-ORACLE-RSP: " + rsp.Replace('\\', '/'));
                    }
                }

                Log($"UCL-ORACLE-END count={names.Count}");
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                exitCode = 1;
            }

            EditorApplication.Exit(exitCode);
        }

        private static string Arg(string[] args, string name)
        {
            var i = Array.IndexOf(args, name);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
        }

        private static void Log(string message) =>
            Debug.LogFormat(LogType.Log, LogOption.NoStacktrace, null, "{0}", message);
    }
}
