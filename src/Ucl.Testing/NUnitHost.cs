using System.Reflection;
using System.Text.RegularExpressions;
using NUnit;
using NUnit.Framework.Api;
using NUnit.Framework.Interfaces;
using NUnit.Framework.Internal;
using Ucl.Core.Testing;
using TestCaseResult = Ucl.Core.Testing.TestCaseResult;

namespace Ucl.Testing;

/// <summary>
/// Discovers and runs NUnit tests with NUnit's own framework API (the one NUnitLite drives), in a collectible
/// <see cref="TestLoadContext"/> (docs/test.md, "Test host"). Unity-only cases, and cases that construct an engine
/// type with a finalizer, are discovered and classified but never run; every other case runs and is classified by its
/// result (<see cref="TestClassifier"/>). Runs inside the test host process (<see cref="TestHost"/>).
/// </summary>
public static class NUnitHost
{
    /// <summary>Runs <paramref name="tests"/>, reporting each case to <paramref name="events"/> as soon as it is known.</summary>
    /// <param name="tests">The test assemblies.</param>
    /// <param name="paths">Every assembly file by simple name: project images, plugin and editor DLLs.</param>
    /// <param name="filter">Only cases whose full name matches (null: all).</param>
    /// <param name="skip">Cases (<see cref="Key"/>) already reported by an earlier host: discovered, not run or reported.</param>
    /// <param name="events">Receives every discovered case, each case as it starts, and each result.</param>
    /// <returns>The number of cases discovered (after <paramref name="filter"/>, skipped ones included).</returns>
    public static int Run(IReadOnlyList<TestAssemblyImage> tests, IReadOnlyDictionary<string, string> paths, Regex? filter, IReadOnlySet<string> skip, ITestEvents events)
    {
        ArgumentNullException.ThrowIfNull(tests);
        ArgumentNullException.ThrowIfNull(paths);
        ArgumentNullException.ThrowIfNull(skip);
        ArgumentNullException.ThrowIfNull(events);

        var context = new TestLoadContext(paths);
        // Project assemblies: the compiled images, which sit in the test assemblies' folder.
        var imageFolders = tests.Select(t => Path.GetDirectoryName(paths[t.Name])).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var projectNames = paths.Where(p => imageFolders.Contains(Path.GetDirectoryName(p.Value))).Select(p => p.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var discovered = 0;
        var reported = 0;
        try
        {
            foreach (var test in tests.OrderBy(t => t.Name, StringComparer.Ordinal))
            {
                var assembly = context.LoadFromAssemblyName(new AssemblyName(test.Name));
                var runner = new NUnitTestAssemblyRunner(new DefaultTestAssemblyBuilder());
                var root = runner.Load(assembly, new Dictionary<string, object>
                {
                    [FrameworkPackageSettings.NumberOfTestWorkers] = 0,
                    [FrameworkPackageSettings.SynchronousEvents] = true,
                });
                var leaves = Leaves(root).Where(l => filter is null || filter.IsMatch(l.FullName)).ToList();
                discovered += leaves.Count;
                foreach (var leaf in leaves)
                {
                    events.Discovered(Case(test.Name, leaf, TestCategory.Skipped, string.Empty));
                }

                var assemblyAttributes = AttributeTypes(assembly.GetCustomAttributesData());
                var toRun = new Dictionary<string, ITest>(StringComparer.Ordinal);
                foreach (var leaf in leaves)
                {
                    if (skip.Contains(Key(test.Name, leaf.FullName)))
                    {
                        reported++;
                        continue;
                    }

                    events.Scanning(test.Name, leaf.FullName);
                    var scanFailures = new List<string>();
                    void ScanFailed(MethodBase scanned, Exception exception) => scanFailures.Add(
                        $"IL scan {scanned.DeclaringType?.FullName}.{scanned.Name}: {exception.GetType().Name}: {exception.Message}");
                    var method = leaf.Method?.MethodInfo;
                    var attributes = assemblyAttributes
                        .Concat(leaf.TypeInfo?.Type is { } type ? AttributeTypes(type.GetCustomAttributesData()) : [])
                        .Concat(method is null ? [] : AttributeTypes(method.GetCustomAttributesData()));
                    var usesLogAssert = method is not null && IlScanner.Calls(method, TestClassifier.LogAssertType, ScanFailed);
                    TestCaseResult? decided = null;
                    if (TestClassifier.UnityOnlyReason(test.PlayMode, attributes, usesLogAssert) is { } reason)
                    {
                        decided = Case(test.Name, leaf, TestCategory.UnityOnly, reason);
                    }
                    else if (IsExplicit(leaf))
                    {
                        decided = Case(test.Name, leaf, TestCategory.Skipped, "[Explicit]: runs only when selected by name");
                    }
                    else if (method is not null && IlScanner.Constructs(method, HasEngineFinalizer, a => IsProject(a, projectNames), ScanFailed) is { } finalizable)
                    {
                        decided = Case(test.Name, leaf, TestCategory.NeedsUnity, TestClassifier.FinalizerReason(finalizable.FullName ?? finalizable.Name));
                    }

                    if (scanFailures.Count > 0)
                    {
                        decided = Case(test.Name, leaf, TestCategory.Failed, string.Join(" | ", scanFailures.Distinct(StringComparer.Ordinal)));
                    }

                    if (decided is null)
                    {
                        events.Scanned(test.Name, leaf.FullName);
                        toRun.Add(leaf.Id, leaf);
                    }
                    else
                    {
                        events.Finished(decided);
                        reported++;
                    }
                }

                if (toRun.Count == 0)
                {
                    continue;
                }

                var listener = new Listener(test.Name, toRun, events);
                runner.Run(listener, new IdFilter([.. toRun.Keys]));
                reported += listener.Reported;
                if (listener.Reported != toRun.Count)
                {
                    throw new InvalidOperationException($"NUnit reported no result for {toRun.Count - listener.Reported} selected case(s) of {test.Name}");
                }
            }
        }
        finally
        {
            context.Unload();
        }

        if (reported != discovered)
        {
            throw new InvalidOperationException($"zero-loss accounting failed: {discovered} cases discovered, {reported} classified");
        }

        return discovered;
    }

    /// <summary>The identity of a case across host processes: assembly and full name.</summary>
    public static string Key(string assembly, string fullName) => assembly + "|" + fullName;

    // A project assembly: one of the compiled images, not an engine, plugin or runtime DLL.
    private static bool IsProject(Assembly assembly, HashSet<string> projectNames) =>
        !assembly.IsDynamic && projectNames.Contains(assembly.GetName().Name ?? string.Empty);

    // An engine type (UnityEngine*, UnityEditor*) that declares a finalizer, itself or in a base type.
    private static bool HasEngineFinalizer(Type type)
    {
        if (!TestClassifier.IsEngineAssembly(type.Assembly.GetName().Name ?? string.Empty))
        {
            return false;
        }

        for (var t = type; t is not null && t != typeof(object); t = t.BaseType)
        {
            if (t.GetMethod("Finalize", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly, null, Type.EmptyTypes, null) is not null)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Reports each selected case as it starts and finishes.</summary>
    private sealed class Listener(string assembly, Dictionary<string, ITest> selected, ITestEvents events) : ITestListener
    {
        public int Reported { get; private set; }

        public void TestStarted(ITest test)
        {
            if (selected.ContainsKey(test.Id))
            {
                events.Started(assembly, test.FullName);
            }
        }

        public void TestFinished(ITestResult result)
        {
            if (!selected.ContainsKey(result.Test.Id))
            {
                return;
            }

            var state = result.ResultState;
            var category = TestClassifier.FromResult(state.Status.ToString(), state.Label, result.Message);
            var reason = category == TestCategory.Passed ? string.Empty : TestClassifier.FirstLine(result.Message);
            events.Finished(Case(assembly, result.Test, category, reason, (long)(result.Duration * 1000)));
            Reported++;
        }

        public void TestOutput(TestOutput output)
        {
        }

        public void SendMessage(TestMessage message)
        {
        }
    }

    // Best effort: on Windows the files stay locked until the collectible context is collected.
    internal static void TryDelete(string folder)
    {
        try
        {
            Directory.Delete(folder, recursive: true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // Left in the temp folder; the OS cleans it.
        }
    }

    private static TestCaseResult Case(string assembly, ITest test, TestCategory category, string reason, long duration = 0) =>
        new(assembly, test.ClassName ?? test.Parent?.FullName ?? string.Empty, test.FullName, category, reason, duration);

    private static IEnumerable<ITest> Leaves(ITest test) =>
        test.IsSuite ? test.Tests.SelectMany(Leaves) : [test];

    private static bool IsExplicit(ITest test)
    {
        for (var t = test; t is not null; t = t.Parent)
        {
            if (t.RunState == RunState.Explicit)
            {
                return true;
            }
        }

        return false;
    }

    // Attribute type names including base types, so a subclass of a Unity attribute counts too.
    private static List<string> AttributeTypes(IEnumerable<CustomAttributeData> attributes)
    {
        var names = new List<string>();
        foreach (var attribute in attributes)
        {
            try
            {
                for (var type = attribute.AttributeType; type is not null && type != typeof(Attribute); type = type.BaseType)
                {
                    names.Add(type.FullName ?? type.Name);
                }
            }
            catch (Exception e) when (e is TypeLoadException or FileNotFoundException)
            {
                // An attribute whose type cannot load cannot be one of the Unity Test Framework's.
            }
        }

        return names;
    }

    /// <summary>Selects test cases by id; suites pass when they contain a selected case.</summary>
    private sealed class IdFilter(HashSet<string> ids) : TestFilter
    {
        public override bool Match(ITest test) => ids.Contains(test.Id);

        public override bool IsExplicitMatch(ITest test) => false;

        public override TNode AddToXml(TNode parentNode, bool recursive) => parentNode.AddElement("ucl-ids");
    }
}
