namespace Ucl.Core.Model;

/// <summary>What is being compiled: the Editor's view of the project or a player build.</summary>
public enum TargetKind
{
    /// <summary>What the Unity Editor compiles (Editor assemblies included, <c>UNITY_EDITOR</c> set).</summary>
    Editor,

    /// <summary>What a player build compiles (Editor assemblies and folders excluded).</summary>
    Player,
}
