using Ucl.Core.Model;

namespace Ucl.Core.Bee;

/// <summary>One dag folder (<c>Library/Bee/artifacts/&lt;name&gt;.dag</c>) and the cell its defines describe.</summary>
/// <param name="Name">Folder name.</param>
/// <param name="Cell">The cell, or null when the defines name no supported platform.</param>
/// <param name="Note">Why the dag was not compared, or null.</param>
/// <param name="Assemblies">Assemblies sorted by name.</param>
public sealed record BeeDagResult(string Name, CompileCell? Cell, string? Note, IReadOnlyList<BeeAssemblyResult> Assemblies);
