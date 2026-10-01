namespace Ucl.Core.Parsing;

/// <summary>One asmdef <c>versionDefines</c> entry.</summary>
/// <param name="Name">Resource: a package name or <c>Unity</c>.</param>
/// <param name="Expression">Version expression, see <see cref="Model.VersionRange"/>.</param>
/// <param name="Define">Symbol to define when the expression matches.</param>
public sealed record VersionDefine(string Name, string Expression, string Define);
