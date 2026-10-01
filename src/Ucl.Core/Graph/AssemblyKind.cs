namespace Ucl.Core.Graph;

/// <summary>How an assembly is defined.</summary>
public enum AssemblyKind
{
    /// <summary>One of the four <c>Assembly-CSharp*</c> assemblies.</summary>
    Predefined,

    /// <summary>An assembly definition file.</summary>
    Asmdef,
}
