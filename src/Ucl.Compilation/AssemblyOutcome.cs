using Microsoft.CodeAnalysis;
using Ucl.Core.Results;

namespace Ucl.Compilation;

/// <summary>A compiled assembly plus what its dependents need: the metadata reference and its hash.</summary>
/// <param name="Result">Reportable result.</param>
/// <param name="Reference">Metadata-only image as a reference, or null when compilation failed or was skipped.</param>
/// <param name="ImageHash">SHA-256 of the metadata-only image: the public surface dependents' inputs hash uses.</param>
/// <param name="Image">The emitted image (full with <see cref="CompileSettings.FullImages"/>), or null.</param>
internal sealed record AssemblyOutcome(AssemblyResult Result, MetadataReference? Reference, string ImageHash, byte[]? Image = null);
