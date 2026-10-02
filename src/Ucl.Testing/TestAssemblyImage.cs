namespace Ucl.Testing;

/// <summary>A compiled test assembly to run.</summary>
/// <param name="Name">Assembly name.</param>
/// <param name="Image">The full PE image.</param>
/// <param name="PlayMode">True for a Play Mode assembly (not Editor-only): its cases are unity-only, never run.</param>
public sealed record TestAssemblyImage(string Name, byte[] Image, bool PlayMode);
