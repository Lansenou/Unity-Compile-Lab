using Ucl.Core.Testing;

namespace Ucl.Core.Tests;

/// <summary>docs/test.md, "Classification" and "Skipping what passed".</summary>
public class TestClassifierTests
{
    [Theory]
    [InlineData("Passed", "", TestCategory.Passed)]
    [InlineData("Warning", "", TestCategory.Passed)]
    [InlineData("Skipped", "Ignored", TestCategory.Ignored)]
    [InlineData("Skipped", "Explicit", TestCategory.Skipped)]
    [InlineData("Inconclusive", "", TestCategory.Skipped)]
    [InlineData("Failed", "", TestCategory.Failed)]
    [InlineData("Failed", "Error", TestCategory.Failed)]
    public void NUnit_result_states_map_to_categories(string status, string label, TestCategory expected) =>
        Assert.Equal(expected, TestClassifier.FromResult(status, label, "  Expected: 4\n  But was:  3"));

    [Theory]
    [InlineData("System.Security.SecurityException : ECall methods must be packaged into a system module.")]
    [InlineData("System.MissingMethodException : Method not found: UnityEngine.GameObject::Internal_CreateGameObject")]
    [InlineData("System.EntryPointNotFoundException : Unable to find an entry point named 'x' in DLL '__Internal'.")]
    [InlineData("System.DllNotFoundException : Unable to load shared library '__Internal'")]
    [InlineData("UnityEngine.UnityException : get_isPlaying can only be called from the main thread.")]
    [InlineData("System.TypeInitializationException : The type initializer for 'Game.Registry' threw an exception.\n  ----> System.Security.SecurityException : ECall methods must be packaged into a system module.")]
    [InlineData("OneTimeSetUp: System.Security.SecurityException : ECall methods must be packaged into a system module.")]
    [InlineData("System.TypeLoadException : Could not load type 'UnityEngine.Internal.X' from assembly 'UnityEngine.CoreModule'")]
    public void Engine_call_failures_are_needs_unity(string message)
    {
        Assert.True(TestClassifier.IsEngineFailure(message));
        Assert.Equal(TestCategory.NeedsUnity, TestClassifier.FromResult("Failed", "Error", message));
    }

    [Theory]
    [InlineData("  Expected: 4\n  But was:  3")]
    [InlineData("System.NullReferenceException : Object reference not set to an instance of an object.")]
    [InlineData("System.TypeLoadException : Could not load type 'Game.Missing' from assembly 'Game.Runtime'")]
    [InlineData("the text System.Security.SecurityException appears without a type separator")]
    [InlineData("")]
    public void Other_failures_are_real(string message)
    {
        Assert.False(TestClassifier.IsEngineFailure(message));
        Assert.Equal(TestCategory.Failed, TestClassifier.FromResult("Failed", string.Empty, message));
    }

    [Fact]
    public void Unity_only_rules_in_priority_order()
    {
        Assert.Contains("Play Mode", TestClassifier.UnityOnlyReason(true, [], false), StringComparison.Ordinal);
        Assert.Contains("[UnityTest]", TestClassifier.UnityOnlyReason(false, ["NUnit.Framework.TestAttribute", "UnityEngine.TestTools.UnityTestAttribute"], false), StringComparison.Ordinal);
        Assert.Contains("[UnityPlatform]", TestClassifier.UnityOnlyReason(false, ["UnityEngine.TestTools.UnityPlatformAttribute"], true), StringComparison.Ordinal);
        Assert.Contains("[RequiresPlayMode]", TestClassifier.UnityOnlyReason(false, ["UnityEngine.TestTools.RequiresPlayModeAttribute"], false), StringComparison.Ordinal);
        Assert.Contains("LogAssert", TestClassifier.UnityOnlyReason(false, ["NUnit.Framework.TestAttribute"], true), StringComparison.Ordinal);
        Assert.Null(TestClassifier.UnityOnlyReason(false, ["NUnit.Framework.TestAttribute"], false));
    }

    [Theory]
    [InlineData(null, "")]
    [InlineData("one", "one")]
    [InlineData("  first  \r\nsecond", "first")]
    public void First_line_is_the_reason(string? message, string expected) => Assert.Equal(expected, TestClassifier.FirstLine(message));

    private static TestCaseResult Case(string cls, string name, TestCategory category) => new("A", cls, $"{cls}.{name}", category, string.Empty);

    [Fact]
    public void Unity_filter_lists_only_classes_whose_every_case_passed()
    {
        var cases = new[]
        {
            Case("Ns.Good", "A", TestCategory.Passed),
            Case("Ns.Good", "B", TestCategory.Passed),
            Case("Ns.Mixed", "A", TestCategory.Passed),
            Case("Ns.Mixed", "B", TestCategory.NeedsUnity),
            Case("Ns.Ignored", "A", TestCategory.Ignored),
            Case("Other.Good+Nested", "A", TestCategory.Passed),
        };
        Assert.Equal(["Ns.Good", "Other.Good+Nested"], UnityTestFilter.FullyPassingClasses(cases));
        Assert.Equal(@"!^Ns\.Good\.;!^Other\.Good\+Nested\.", UnityTestFilter.Build(cases));
        Assert.Equal(string.Empty, UnityTestFilter.Build([]));
    }

    [Fact]
    public void A_host_crash_with_an_engine_frame_needs_unity()
    {
        var (category, reason) = TestClassifier.FromHostCrash("Unhandled exception. System.NullReferenceException: Object reference not set\n   at UnityEngine.Rendering.CommandBuffer.Finalize()\n   at System.GC.RunFinalizers()");
        Assert.Equal(TestCategory.NeedsUnity, category);
        Assert.Equal("test host crashed during this case: Unhandled exception. System.NullReferenceException: Object reference not set", reason);
    }

    [Fact]
    public void A_host_crash_without_an_engine_frame_is_a_failure()
    {
        Assert.Equal(TestCategory.Failed, TestClassifier.FromHostCrash("Stack overflow.\n   at Game.Tests.Recursion.Run()").Category);
    }

    [Theory]
    [InlineData("UnityEngine.CoreModule", true)]
    [InlineData("UnityEditor.CoreModule", true)]
    [InlineData("Game.Runtime", false)]
    public void Engine_assemblies_are_the_unity_ones(string name, bool engine) =>
        Assert.Equal(engine, TestClassifier.IsEngineAssembly(name));

    [Fact]
    public void The_finalizer_reason_names_the_type() =>
        Assert.StartsWith("constructs UnityEngine.Rendering.CommandBuffer,", TestClassifier.FinalizerReason("UnityEngine.Rendering.CommandBuffer"), StringComparison.Ordinal);
}
