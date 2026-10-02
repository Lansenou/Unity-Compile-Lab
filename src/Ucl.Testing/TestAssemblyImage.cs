namespace Ucl.Testing;

/// <summary>A compiled test assembly to run.</summary>
/// <param name="Name">Assembly name (its image is in the run's assembly files under this name).</param>
/// <param name="PlayMode">True for a Play Mode assembly (not Editor-only): its cases are unity-only, never run.</param>
public sealed record TestAssemblyImage(string Name, bool PlayMode);
