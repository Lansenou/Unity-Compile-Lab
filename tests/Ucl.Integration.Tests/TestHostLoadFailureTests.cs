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
    [Fact]
    public void Failure_details_keep_initializer_causes_and_actual_exception_types()
    {
        var image = Emit("FailureDetailTests", """
            using NUnit.Framework;
            using System;
            public static class BrokenInitializer {
                public static readonly int Value = Initialize();
                private static int Initialize() => throw new InvalidOperationException("synthetic initializer cause");
            }
            public class Cases {
                [Test] public void A_initializer() { Assert.AreEqual(0, BrokenInitializer.Value); }
                [Test] public void B_wrong_exception() {
                    Assert.Throws<ArgumentException>(() => throw new InvalidOperationException("synthetic actual exception"));
                }
                [Test] public void Z_after() { Assert.AreEqual(4, 2 + 2); }
            }
            """);
        var run = TestHost.Run([new("FailureDetailTests", false)], new Dictionary<string, byte[]> { ["FailureDetailTests"] = image },
            new Dictionary<string, string>(), null, Launch);
        Assert.Empty(run.Crashes);
        Assert.Equal(3, run.Discovered);
        var initializer = Assert.Single(run.Cases, c => c.FullName == "Cases.A_initializer");
        Assert.Equal(TestCategory.Failed, initializer.Category);
        Assert.Contains("TypeInitializationException", initializer.Reason, StringComparison.Ordinal);
        Assert.Contains("InvalidOperationException", initializer.Reason, StringComparison.Ordinal);
        Assert.Contains("synthetic initializer cause", initializer.Reason, StringComparison.Ordinal);
        var wrong = Assert.Single(run.Cases, c => c.FullName == "Cases.B_wrong_exception");
        Assert.Equal(TestCategory.Failed, wrong.Category);
        Assert.Contains("Expected:", wrong.Reason, StringComparison.Ordinal);
        Assert.Contains("But was:", wrong.Reason, StringComparison.Ordinal);
        Assert.Contains("System.InvalidOperationException", wrong.Reason, StringComparison.Ordinal);
        Assert.Equal(TestCategory.Passed, Assert.Single(run.Cases, c => c.FullName == "Cases.Z_after").Category);
        var report = new TestRunReport { ToolVersion = "test", Cases = run.Cases };
        foreach (var format in new[] { "text", "json", "junit", "nunit3" })
        {
            var output = Ucl.Reporting.TestReport.Render(report, format);
            Assert.Contains("synthetic initializer cause", output, StringComparison.Ordinal);
            Assert.Contains("But was:", output, StringComparison.Ordinal);
        }
    }

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
