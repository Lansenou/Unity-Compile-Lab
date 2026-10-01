namespace Ucl.Core.Model;

/// <summary>A configuration problem (exit code 3): missing editor, unresolved package, bad asmdef, unsupported version.</summary>
/// <param name="Id">Problem id (<c>UCL3xxx</c>).</param>
/// <param name="Message">What is wrong and how to fix it.</param>
/// <param name="File">Project-relative file the problem is about, or null.</param>
public sealed record Problem(string Id, string Message, string? File = null);
