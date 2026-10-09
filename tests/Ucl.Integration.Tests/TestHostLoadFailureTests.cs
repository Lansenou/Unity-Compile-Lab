using System.Runtime.InteropServices;
using System.Text.Json;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Ucl.Core.Testing;
using Ucl.Testing;
using Xunit;

namespace Ucl.Integration.Tests;

/// <summary>Original synthetic assemblies reproduce missing method-body types without external binaries.</summary>
public sealed class TestHostLoadFailureTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void A_missing_method_body_type_is_reported_and_later_cases_run(bool inHelper)
    {
        using var temp = new TempDir();
        var dependency = Emit("BodyDependency", "public struct MissingBodyType { public int Value; }");
        var source = """
            using NUnit.Framework;
            public class Cases {
                [Test] public void A_bad_body() { MissingBodyType value = default; Assert.That(value.Value, Is.Zero); }
                [Test] public void Z_after() { Assert.That(2 + 2, Is.EqualTo(4)); }
            }
            """;
        if (inHelper)
        {
            source = source.Replace("MissingBodyType value = default; Assert.That(value.Value, Is.Zero);", "Helper.Broken();", StringComparison.Ordinal)
                + " public static class Helper { public static void Broken() { MissingBodyType value = default; Assert.That(value.Value, Is.Zero); } }";
        }
        var image = Emit("BodyTests", source, MetadataReference.CreateFromImage(dependency));
        // Same assembly identity, but the type compiled into the test's local signature is absent.
        var path = Path.Combine(temp.Path, "BodyDependency.dll");
        File.WriteAllBytes(path, Emit("BodyDependency", "public struct Replacement { }"));
        var run = TestHost.Run([new("BodyTests", false)], new Dictionary<string, byte[]> { ["BodyTests"] = image },
            new Dictionary<string, string> { ["BodyDependency"] = path }, null, Launch);
        Assert.Equal(2, run.Discovered);
        Assert.Empty(run.Crashes);
        var bad = Assert.Single(run.Cases, c => c.FullName == "Cases.A_bad_body");
        Assert.Equal(TestCategory.NeedsUnity, bad.Category);
        Assert.Contains("IL scan", bad.Reason, StringComparison.Ordinal);
        Assert.Contains(inHelper ? "Helper.Broken" : "Cases.A_bad_body", bad.Reason, StringComparison.Ordinal);
        Assert.Contains("TypeLoadException", bad.Reason, StringComparison.Ordinal);
        Assert.Contains("MissingBodyType", bad.Reason, StringComparison.Ordinal);
        Assert.Equal(TestCategory.Passed, Assert.Single(run.Cases, c => c.FullName == "Cases.Z_after").Category);
    }

    [Fact]
    public void A_crash_during_classification_is_an_error_and_resumes_the_next_case()
    {
        var image = Emit("ResumeTests", "using NUnit.Framework; public class Cases { [Test] public void A_scan() {} [Test] public void Z_after() {} }");
        var launches = 0;
        var run = TestHost.Run([new("ResumeTests", false)], new Dictionary<string, byte[]> { ["ResumeTests"] = image },
            new Dictionary<string, string>(), null, args =>
            {
                launches++;
                if (launches != 1) return Launch(args);
                using var request = JsonDocument.Parse(File.ReadAllText(args[0]));
                var cases = new[] { "Cases.A_scan", "Cases.Z_after" }.Select(n => new TestCaseResult("ResumeTests", "Cases", n, TestCategory.Skipped, "")).ToArray();
                var lines = cases.Select(c => JsonSerializer.Serialize(new { Event = "discovered", Case = c })).ToList();
                lines.Add(JsonSerializer.Serialize(new { Event = "scanning", Case = cases[0] }));
                File.WriteAllLines(request.RootElement.GetProperty("Results").GetString()!, lines);
                return (1, "Unhandled exception. System.TypeLoadException: synthetic scan crash\n   at UnityEngine.Synthetic.Frame()");
            });
        Assert.Equal(2, launches);
        Assert.Equal(2, run.Discovered);
        Assert.Equal(TestCategory.Failed, run.Cases[0].Category);
        Assert.Equal(TestCategory.Passed, run.Cases[1].Category);
        Assert.Equal("Cases.A_scan", Assert.Single(run.Crashes).During);
    }


    [Fact]
    public void Readonly_static_reflection_is_a_runtime_divergence_and_later_cases_run()
    {
        var image = Emit("ReadonlyTests", """
            using NUnit.Framework;
            using System.Reflection;
            public class Cases {
                private static readonly int Value = 1;
                [Test] public void A_initialized_readonly() {
                    Assert.AreEqual(1, Value);
                    typeof(Cases).GetField("Value", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, 2);
                }
                [Test] public void Z_after() { Assert.AreEqual(1, 1); }
            }
            """);
        var run = TestHost.Run([new("ReadonlyTests", false)], new Dictionary<string, byte[]> { ["ReadonlyTests"] = image },
            new Dictionary<string, string>(), null, Launch);
        Assert.Empty(run.Crashes);
        Assert.Equal(2, run.Discovered);
        Assert.True(run.Cases[0].Category == TestCategory.NeedsUnity, run.Cases[0].Reason);
        Assert.Contains("readonly", run.Cases[0].Reason, StringComparison.Ordinal);
        Assert.Equal(TestCategory.Passed, run.Cases[1].Category);
    }


    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void Unity_only_cases_do_not_scan_unloadable_method_bodies(bool playMode, bool unityTest)
    {
        using var temp = new TempDir();
        var dependency = Emit("ExcludedBodyDependency", "public struct MissingBodyType { public int Value; }");
        var source = """
            using NUnit.Framework;
            public class Cases {
                [Test] public void A_bad_body() { MissingBodyType value = default; Assert.AreEqual(0, value.Value); }
                [Test] public void Z_after() { Assert.AreEqual(1, 1); }
            }
            namespace UnityEngine.TestTools { public class UnityTestAttribute : System.Attribute { } }
            """;
        if (unityTest) source = source.Replace("[Test] public void A_bad_body", "[Test, UnityEngine.TestTools.UnityTest] public void A_bad_body", StringComparison.Ordinal);
        var image = Emit("ExcludedBodyTests", source, MetadataReference.CreateFromImage(dependency));
        var path = Path.Combine(temp.Path, "ExcludedBodyDependency.dll");
        File.WriteAllBytes(path, Emit("ExcludedBodyDependency", "public struct Replacement { }"));
        var run = TestHost.Run([new("ExcludedBodyTests", playMode)], new Dictionary<string, byte[]> { ["ExcludedBodyTests"] = image },
            new Dictionary<string, string> { ["ExcludedBodyDependency"] = path }, null, Launch);
        Assert.Empty(run.Crashes);
        Assert.Equal(2, run.Discovered);
        Assert.Equal(TestCategory.UnityOnly, run.Cases[0].Category);
        Assert.DoesNotContain("IL scan", run.Cases[0].Reason, StringComparison.Ordinal);
        Assert.Equal(playMode ? TestCategory.UnityOnly : TestCategory.Passed, run.Cases[1].Category);
    }

    [Fact]
    public void Audited_case_is_not_executed_and_same_named_failure_in_other_assembly_stays_red()
    {
        const string source = "using NUnit.Framework; public class Cases { [Test] public void A_mismatch() { Assert.Fail(\"real failure\"); } [Test] public void Z_after() {} }";
        var images = new Dictionary<string, byte[]> { ["FirstTests"] = Emit("FirstTests", source), ["SecondTests"] = Emit("SecondTests", source) };
        var run = TestHost.Run([new("FirstTests", false), new("SecondTests", false)], images,
            new Dictionary<string, string>(), null, Launch,
            new HashSet<string>(StringComparer.Ordinal) { NUnitHost.Key("FirstTests", "Cases.A_mismatch") });
        Assert.Equal(4, run.Discovered);
        Assert.Empty(run.Crashes);
        Assert.Equal(TestCategory.NeedsUnity, Assert.Single(run.Cases, c => c.Assembly == "FirstTests" && c.FullName == "Cases.A_mismatch").Category);
        Assert.Equal(TestCategory.Failed, Assert.Single(run.Cases, c => c.Assembly == "SecondTests" && c.FullName == "Cases.A_mismatch").Category);
        Assert.Equal(2, run.Cases.Count(c => c.Category == TestCategory.Passed));
    }

    [Fact]
    public void Images_load_under_the_given_root_so_TestDirectory_follows_the_project_layout()
    {
        using var temp = new TempDir();
        var root = Path.Combine(temp.Path, "Project", "Library", "ucl");
        var source = "using NUnit.Framework; public class Cases { [Test] public void Where() { Assert.That(System.IO.Path.GetDirectoryName(TestContext.CurrentContext.TestDirectory), Is.EqualTo(@\""
            + root + "\")); } }";
        var run = TestHost.Run([new("LayoutTests", false)], new Dictionary<string, byte[]> { ["LayoutTests"] = Emit("LayoutTests", source) },
            new Dictionary<string, string>(), null, Launch, imageRoot: root);
        Assert.Empty(run.Crashes);
        var where = Assert.Single(run.Cases);
        Assert.True(where.Category == TestCategory.Passed, where.Reason);
    }

    [Theory]
    [InlineData(true, "", "", "[UnityPlatform]")]
    [InlineData(false, "[UnityPlatform]", "", "[UnityTest]")]
    [InlineData(false, "", "[assembly: UnityEngine.TestTools.UnityPlatform]", "[UnityTest]")]
    [InlineData(false, "", "", "[UnityPlatform, UnityEngine.TestTools.RequiresPlayMode]")]
    public void Platform_cases_stay_with_the_Editor_whatever_other_reason_classifies_them(bool playMode, string classAttribute, string assemblyAttribute, string methodAttributes)
    {
        var source = "using NUnit.Framework; using UnityEngine.TestTools; " + assemblyAttribute + " " + classAttribute
            + " public class Cases { [Test] " + methodAttributes + " public void A_platform() {} [Test] public void Z_other() {} }"
            + " namespace UnityEngine.TestTools { public class UnityTestAttribute : System.Attribute { } public class UnityPlatformAttribute : System.Attribute { }"
            + " public class RequiresPlayModeAttribute : System.Attribute { } }";
        source = source.Replace("[UnityTest]", "[UnityEngine.TestTools.UnityTest]", StringComparison.Ordinal);
        var run = TestHost.Run([new("PlatformTests", playMode)], new Dictionary<string, byte[]> { ["PlatformTests"] = Emit("PlatformTests", source) },
            new Dictionary<string, string>(), null, Launch);
        Assert.Empty(run.Crashes);
        var platform = Assert.Single(run.Cases, c => c.FullName == "Cases.A_platform");
        Assert.True(platform.EditorPlatform);
        var candidate = typeof(Ucl.Cli.CliOptions).Assembly.GetType("Ucl.Cli.ProjectPlayerTest", throwOnError: true)!
            .GetMethod("PlayerCandidate", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
        Assert.False((bool)candidate.Invoke(null, [platform])!);
        var other = Assert.Single(run.Cases, c => c.FullName == "Cases.Z_other");
        Assert.Equal(classAttribute.Length > 0 || assemblyAttribute.Length > 0, other.EditorPlatform);
        var report = new TestRunReport { ToolVersion = "test", Cases = [platform with { Route = "needs-editor" }] };
        Assert.Equal("Cases.A_platform\n", Ucl.Reporting.TestReport.UnityTestList(report));
    }

    private static (int Exit, string Error) Launch(IReadOnlyList<string> args)
    {
        try { return (TestHost.Serve(args[0]), ""); }
        catch (Exception e) { return (1, e.ToString()); }
    }

    private static byte[] Emit(string name, string source, params MetadataReference[] extra)
    {
        var references = new[] { "System.Private.CoreLib.dll", "System.Runtime.dll", "netstandard.dll" }
            .Select(n => MetadataReference.CreateFromFile(Path.Combine(RuntimeEnvironment.GetRuntimeDirectory(), n)))
            .Cast<MetadataReference>().Concat([MetadataReference.CreateFromFile(typeof(NUnit.Framework.Assert).Assembly.Location), .. extra]);
        var compilation = CSharpCompilation.Create(name, [CSharpSyntaxTree.ParseText(source)], references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, optimizationLevel: OptimizationLevel.Debug));
        using var stream = new MemoryStream();
        var result = compilation.Emit(stream);
        Assert.True(result.Success, string.Join("\n", result.Diagnostics));
        return stream.ToArray();
    }
}
