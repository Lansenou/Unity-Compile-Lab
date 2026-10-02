using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;

public static class PlayerTestHost
{
    [Serializable] public class Case { public string name; public bool passed; public string exception; }
    [Serializable] public class Report
    {
        public string assembly;
        public long firstTestUnixMs;
        public double bootToFirstTestMs;
        public string dataPath;
        public List<Case> tests = new List<Case>();
        public string fatal;
    }

    static string Arg(string name)
    {
        var args = Environment.GetCommandLineArgs();
        var i = Array.IndexOf(args, name);
        return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
    }
    static bool Has(MethodInfo method, string attribute)
    {
        return method.GetCustomAttributes(false).Any(a => a.GetType().FullName == "NUnit.Framework." + attribute);
    }
    static string ExceptionText(Exception error)
    {
        while (error is TargetInvocationException && error.InnerException != null) error = error.InnerException;
        return error.ToString();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    public static void Run()
    {
        // Ensure required built-in modules ship even though the test DLL is unknown at build time.
        GC.KeepAlive(typeof(BoxCollider));
        GC.KeepAlive(typeof(Texture2D));
        var report = new Report { dataPath = Application.dataPath };
        var output = Arg("-results");
        if (output == null) return;
        try
        {
            var dll = Path.GetFullPath(Arg("-testAssembly"));
            AppDomain.CurrentDomain.AssemblyResolve += (sender, e) =>
            {
                var candidate = Path.Combine(Path.GetDirectoryName(dll), new AssemblyName(e.Name).Name + ".dll");
                return File.Exists(candidate) ? Assembly.LoadFrom(candidate) : null;
            };
            var assembly = Assembly.LoadFrom(dll);
            report.assembly = assembly.FullName;
            var methods = assembly.GetTypes()
                .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static))
                .Where(m => Has(m, "TestAttribute")).OrderBy(m => m.Name).ToArray();
            foreach (var method in methods)
            {
                var result = new Case { name = method.DeclaringType.FullName + "." + method.Name };
                object fixture = null;
                if (report.firstTestUnixMs == 0)
                {
                    report.firstTestUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                    if (long.TryParse(Arg("-startUtc"), out var start)) report.bootToFirstTestMs = report.firstTestUnixMs - start;
                    Debug.Log("SPIKE_FIRST_TEST " + report.firstTestUnixMs);
                }
                try
                {
                    fixture = method.IsStatic ? null : Activator.CreateInstance(method.DeclaringType);
                    var lifecycle = method.DeclaringType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
                    foreach (var setup in lifecycle.Where(m => Has(m, "SetUpAttribute"))) setup.Invoke(fixture, null);
                    method.Invoke(fixture, null);
                    result.passed = true;
                }
                catch (Exception error) { result.exception = ExceptionText(error); }
                finally
                {
                    if (fixture != null)
                    {
                        try
                        {
                            foreach (var teardown in method.DeclaringType.GetMethods().Where(m => Has(m, "TearDownAttribute")))
                                teardown.Invoke(fixture, null);
                        }
                        catch (Exception error) { result.passed = false; result.exception += "\nTearDown: " + ExceptionText(error); }
                    }
                }
                report.tests.Add(result);
            }
        }
        catch (Exception error) { report.fatal = ExceptionText(error); }
        File.WriteAllText(output, JsonUtility.ToJson(report, true));
        Debug.Log("SPIKE_FINISHED " + report.tests.Count);
        Application.Quit(0);
    }
}
