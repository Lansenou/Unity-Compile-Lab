namespace Ucl.Core.Rules;

/// <summary>
/// Unity's code-gen (IL post-processor) assembly names (UnityCsReference <c>UnityCodeGenHelpers</c> and
/// <c>CompilationPipelineCommonHelper</c>): <c>Unity.*.CodeGen</c> and <c>Unity.*.Compiler</c>, their <c>.Tests</c>, and
/// <c>Unity.*.Compiler.Client</c>. Case-insensitive, as Unity compares them.
/// </summary>
public static class CodeGenAssemblies
{
    /// <summary>True for <c>Unity.*.CodeGen</c> and <c>Unity.*.Compiler</c>: no auto-referenced UI and no test runner references.</summary>
    public static bool IsCodeGen(string name) => Matches(name, ".CodeGen") || Matches(name, ".Compiler");

    /// <summary>True for the assemblies that also reference <c>Managed/Unity.CompilationPipeline.Common.dll</c>.</summary>
    public static bool UsesCompilationPipeline(string name) =>
        IsCodeGen(name) || Matches(name, ".CodeGen.Tests") || Matches(name, ".Compiler.Tests") || Matches(name, ".Compiler.Client");

    private static bool Matches(string name, string suffix) =>
        name.StartsWith("Unity.", StringComparison.OrdinalIgnoreCase) && name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase);
}
