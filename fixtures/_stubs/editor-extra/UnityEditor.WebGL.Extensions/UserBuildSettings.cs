// Original stand-in for part of the Unity API, written for the ucl fixtures. Not Unity code. Apache-2.0.

namespace UnityEditor.WebGL
{
    /// <summary>WebAssembly code optimization of a Web build.</summary>
    public enum WasmCodeOptimization
    {
        BuildTimes = 0,
        RuntimeSpeed = 1,
        DiskSize = 2,
    }

    /// <summary>User build settings of the Web platform.</summary>
    public static class UserBuildSettings
    {
        public static WasmCodeOptimization codeOptimization { get => throw null; set => throw null; }
    }
}
