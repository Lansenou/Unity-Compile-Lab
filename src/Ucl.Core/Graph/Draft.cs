using Ucl.Core.Parsing;
using Ucl.Core.Rules;

namespace Ucl.Core.Graph;

/// <summary>An assembly being planned; mutable while references are resolved, then frozen into an <see cref="AssemblyPlan"/>.</summary>
/// <param name="Name">Assembly name.</param>
/// <param name="Kind">Predefined or asmdef.</param>
/// <param name="Entry">asmdef entry, or null for predefined assemblies.</param>
/// <param name="Defines">Defines of this assembly.</param>
/// <param name="Rsp">Response file applied (path null when none).</param>
/// <param name="AllowUnsafe">Unsafe code allowed before response-file options.</param>
/// <param name="IsEditorOnly">Only exists in the Editor.</param>
internal sealed record Draft(string Name, AssemblyKind Kind, AsmdefEntry? Entry, DefineSet Defines, (string? Path, RspOptions? Options) Rsp, bool AllowUnsafe, bool IsEditorOnly)
{
    /// <summary>Referenced assembly names compiled in the cell.</summary>
    public List<string> References { get; } = [];

    /// <summary>References that exist but are not compiled in the cell.</summary>
    public List<string> Dropped { get; } = [];
}
