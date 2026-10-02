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
/// Discovers and runs NUnit tests in process with NUnit's own framework API (the one NUnitLite drives), in a
/// collectible <see cref="TestLoadContext"/> (docs/test.md, "Test host"). Unity-only cases are discovered and counted
/// but never run; every other case runs and is classified by its result (<see cref="TestClassifier"/>).
/// </summary>
public static class NUnitHost
{
    /// <summary>Runs <paramref name="tests"/>.</summary>
    /// <param name="tests">The test assemblies, with their images.</param>
    /// <param name="images">Every project assembly image by name (test assemblies and their dependencies); written to a temporary folder to load.</param>
    /// <param name="files">Plugin and editor DLLs by assembly simple name.</param>
    /// <param name="filter">Only cases whose full name matches (null: all).</param>
    public static TestRun Run(IReadOnlyList<TestAssemblyImage> tests, IReadOnlyDictionary<string, byte[]> images, IReadOnlyDictionary<string, string> files, Regex? filter)
    {
        ArgumentNullException.ThrowIfNull(tests);
        ArgumentNullException.ThrowIfNull(images);
        ArgumentNullException.ThrowIfNull(files);

        // Assemblies are loaded from files, never from memory: NUnit asks for an assembly's path, which a
        // single-file host cannot give for an assembly without one. The folder is private to this run and outside
        // the project (the read-only contract), and is deleted afterwards.
        var folder = Path.Combine(Path.GetTempPath(), "ucl-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        var paths = new Dictionary<string, string>(files, StringComparer.OrdinalIgnoreCase);
        foreach (var (name, image) in images)
        {
            var path = Path.Combine(folder, name + ".dll");
            File.WriteAllBytes(path, image);
            paths[name] = path;
        }

        var context = new TestLoadContext(paths);
        var cases = new List<TestCaseResult>();
        var discovered = 0;
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
                var assemblyAttributes = AttributeTypes(assembly.GetCustomAttributesData());

                var toRun = new HashSet<string>(StringComparer.Ordinal);
                foreach (var leaf in leaves)
                {
                    var method = leaf.Method?.MethodInfo;
                    var attributes = assemblyAttributes
                        .Concat(leaf.TypeInfo?.Type is { } type ? AttributeTypes(type.GetCustomAttributesData()) : [])
                        .Concat(method is null ? [] : AttributeTypes(method.GetCustomAttributesData()));
                    var usesLogAssert = method is not null && IlScanner.Calls(method, TestClassifier.LogAssertType);
                    if (TestClassifier.UnityOnlyReason(test.PlayMode, attributes, usesLogAssert) is { } reason)
                    {
                        cases.Add(Case(test.Name, leaf, TestCategory.UnityOnly, reason));
                    }
                    else if (IsExplicit(leaf))
                    {
                        cases.Add(Case(test.Name, leaf, TestCategory.Skipped, "[Explicit]: runs only when selected by name"));
                    }
                    else
                    {
                        toRun.Add(leaf.Id);
                    }
                }

                if (toRun.Count == 0)
                {
                    continue;
                }

                var result = runner.Run(TestListener.NULL, new IdFilter(toRun));
                foreach (var leaf in LeafResults(result).Where(r => toRun.Contains(r.Test.Id)))
                {
                    var state = leaf.ResultState;
                    var category = TestClassifier.FromResult(state.Status.ToString(), state.Label, leaf.Message);
                    var reason = category == TestCategory.Passed ? string.Empty : TestClassifier.FirstLine(leaf.Message);
                    cases.Add(Case(test.Name, leaf.Test, category, reason, (long)(leaf.Duration * 1000)));
                    toRun.Remove(leaf.Test.Id);
                }

                if (toRun.Count > 0)
                {
                    throw new InvalidOperationException($"NUnit reported no result for {toRun.Count} selected case(s) of {test.Name}");
                }
            }
        }
        finally
        {
            context.Unload();
            TryDelete(folder);
        }

        if (cases.Count != discovered)
        {
            throw new InvalidOperationException($"zero-loss accounting failed: {discovered} cases discovered, {cases.Count} classified");
        }

        return new TestRun(
            [.. cases.OrderBy(c => c.Assembly, StringComparer.Ordinal).ThenBy(c => c.FullName, StringComparer.Ordinal)],
            discovered);
    }

    // Best effort: on Windows the files stay locked until the collectible context is collected.
    private static void TryDelete(string folder)
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

    private static IEnumerable<ITestResult> LeafResults(ITestResult result) =>
        result.Test.IsSuite ? result.Children.SelectMany(LeafResults) : [result];

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
