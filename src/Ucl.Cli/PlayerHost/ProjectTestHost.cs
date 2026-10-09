using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework.Interfaces;
using NUnit.Framework.Internal;
using UnityEngine;

public sealed class ProjectTestHost : MonoBehaviour, ITestListener
{
    [Serializable] public sealed class Case { public string assembly; public string name; public string outcome; public string message; public double seconds; }
    [Serializable] public sealed class Report
    {
        public long firstTestUnixMs;
        public long finishedUnixMs;
        public string fatal = "";
        public string runner = "UTF internal player runner";
        public List<Case> tests = new List<Case>();
    }
    readonly Report report = new Report();
    string output;
    public static string Arg(string name)
    {
        var args = Environment.GetCommandLineArgs();
        var i = Array.IndexOf(args, name);
        return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (Arg("-results") == null) return;
        var go = new GameObject("Project test host");
        DontDestroyOnLoad(go);
        go.AddComponent<ProjectTestHost>();
    }
    IEnumerator Start()
    {
        output = Arg("-results");
        List<IGrouping<string, Assembly>> groups;
        Func<string, Type> type;
        try
        {
            var directory = Path.GetFullPath(Arg("-assemblyDirectory"));
            AppDomain.CurrentDomain.AssemblyResolve += (sender, e) =>
            {
                var path = Path.Combine(directory, new AssemblyName(e.Name).Name + ".dll");
                return File.Exists(path) ? Assembly.LoadFrom(path) : null;
            };
            // "<EditMode|PlayMode>\t<path>" per line. UTF keeps one TestMode per run (UnityTestExecutionContext), which
            // rejects Edit Mode yield instructions in Play Mode tests, so each platform loads and runs on its own, as in the Editor.
            groups = File.ReadAllLines(Arg("-assemblies")).Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s.Split('\t'))
                .GroupBy(s => s[0], s => Assembly.LoadFrom(s[1])).ToList();
            var utf = typeof(UnityEngine.TestTools.LogAssert).Assembly;
            type = n => utf.GetType(n, true);
            type("UnityEngine.TestRunner.NUnitExtensions.Runner.CoroutineTestWorkItem").GetProperty("monoBehaviourCoroutineRunner").SetValue(null, this);
        }
        catch (Exception e) { report.fatal = e.ToString(); Finish(); yield break; }
        foreach (var group in groups)
        {
            object runner;
            IEnumerator steps;
            try
            {
                var builder = Activator.CreateInstance(type("UnityEngine.TestTools.NUnitExtensions.UnityTestAssemblyBuilder"),
                    new object[] { null, 0 });
                var factory = Activator.CreateInstance(type("UnityEngine.TestRunner.NUnitExtensions.Runner.PlaymodeWorkItemFactory"), true);
                var contextType = type("UnityEngine.TestRunner.NUnitExtensions.Runner.UnityTestExecutionContext");
                var context = Activator.CreateInstance(contextType);
                contextType.GetProperty("CurrentContext").SetValue(null, context);
                contextType.GetProperty("Automated").SetValue(context, true);
                contextType.GetProperty("FeatureFlags").SetValue(context,
                    Activator.CreateInstance(type("UnityEngine.TestRunner.NUnitExtensions.Runner.FeatureFlags"), true));
                runner = Activator.CreateInstance(type("UnityEngine.TestRunner.NUnitExtensions.Runner.UnityTestAssemblyRunner"),
                    new[] { builder, factory, context });
                var platform = Enum.Parse(type("UnityEngine.TestTools.TestPlatform"), group.Key);
                var tree = (ITest)runner.GetType().GetMethod("Load").Invoke(runner,
                    new object[] { group.ToArray(), platform, new Dictionary<string, object>() });
                if (Arg("-discover") != null) { File.WriteAllText(output + "." + group.Key + ".discovery.xml", tree.ToXml(true).OuterXml); continue; }
                var filter = new ExactFilter(new HashSet<string>(File.ReadAllLines(Arg("-cases")), StringComparer.Ordinal));
                steps = ((IEnumerable)runner.GetType().GetMethod("Run").Invoke(runner, new object[] { this, filter })).GetEnumerator();
            }
            catch (Exception e) { report.fatal = e.ToString(); Finish(); yield break; }
            while (true)
            {
                bool more;
                try { more = steps.MoveNext(); }
                catch (Exception e) { report.fatal = e.ToString(); break; }
                if (!more) break;
                yield return steps.Current;
            }
            var result = (ITestResult)runner.GetType().GetProperty("Result").GetValue(runner);
            if (result != null) File.WriteAllText(output + "." + group.Key + ".nunit.xml", result.ToXml(true).OuterXml);
            if (report.fatal.Length > 0) break;
        }
        Finish();
    }
    void Finish()
    {
        report.finishedUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        // A kill mid-write leaves no results.json rather than a torn one; the parent then reads the stream.
        File.WriteAllText(output + ".tmp", JsonUtility.ToJson(report, true));
        if (File.Exists(output)) File.Delete(output);
        File.Move(output + ".tmp", output);
        Application.Quit(report.fatal.Length == 0 && !report.tests.Any(t => t.outcome == "Failed") ? 0 : 1);
    }
    public void TestStarted(ITest test)
    {
        if (test.IsSuite) return;
        if (report.firstTestUnixMs == 0) report.firstTestUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        File.WriteAllText(output + ".started", AssemblyName(test) + "\n" + test.FullName);
    }
    public void TestFinished(ITestResult result)
    {
        if (result.Test.IsSuite) return;
        report.tests.Add(new Case { assembly = AssemblyName(result.Test), name = result.Test.FullName, outcome = result.ResultState.Status.ToString(),
            message = result.Message ?? "", seconds = result.Duration });
        File.AppendAllText(output + ".jsonl", JsonUtility.ToJson(report.tests[report.tests.Count - 1]) + "\n");
    }
    public void TestOutput(TestOutput output) { }
    static string AssemblyName(ITest test)
    {
        while (test != null)
        {
            if (test.TypeInfo != null) return test.TypeInfo.Type.Assembly.GetName().Name;
            test = test.Parent;
        }
        return "";
    }
    sealed class ExactFilter : TestFilter
    {
        readonly HashSet<string> names;
        public ExactFilter(HashSet<string> names) { this.names = names; }
        public override bool Match(ITest test) { return names.Contains(AssemblyName(test) + "\t" + test.FullName); }
        public override TNode AddToXml(TNode parentNode, bool recursive) { return parentNode.AddElement("filter"); }
    }
}
