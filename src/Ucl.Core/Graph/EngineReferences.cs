namespace Ucl.Core.Graph;

/// <summary>Which editor DLLs an assembly references.</summary>
public enum EngineReferences
{
    /// <summary>None (<c>noEngineReferences</c>).</summary>
    None,

    /// <summary>UnityEngine module DLLs (player cells).</summary>
    Runtime,

    /// <summary>UnityEngine and UnityEditor DLLs (editor cells).</summary>
    RuntimeAndEditor,
}
