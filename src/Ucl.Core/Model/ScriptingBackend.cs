namespace Ucl.Core.Model;

/// <summary>The scripting backend, which selects <c>ENABLE_MONO</c> or <c>ENABLE_IL2CPP</c>.</summary>
public enum ScriptingBackend
{
    /// <summary>Mono (PlayerSettings value 0).</summary>
    Mono,

    /// <summary>IL2CPP (PlayerSettings value 1).</summary>
    IL2CPP,
}
