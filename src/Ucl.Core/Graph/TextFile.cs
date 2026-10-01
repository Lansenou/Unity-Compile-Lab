namespace Ucl.Core.Graph;

/// <summary>A project file handed to the graph builder: its logical path, its text, and its <c>.meta</c> text when it has one.</summary>
/// <param name="Path">Project-relative path with <c>/</c> separators (<c>Packages/&lt;name&gt;/...</c> for package files).</param>
/// <param name="Text">File content (empty for binary files such as DLLs).</param>
/// <param name="MetaText">Content of the <c>.meta</c> beside it, or null.</param>
public sealed record TextFile(string Path, string Text, string? MetaText = null);
