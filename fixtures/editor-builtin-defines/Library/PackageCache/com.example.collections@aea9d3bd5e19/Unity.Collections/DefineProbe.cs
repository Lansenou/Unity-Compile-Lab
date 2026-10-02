// Fixture source for the ucl conformance corpus. Original code, Apache-2.0.

// Each line states a rule of docs/defines.md ("Built-in symbols"); a missing symbol stops the compile with CS1029.
#if !CSHARP_7_OR_LATER
#error CSHARP_7_OR_LATER is defined for every assembly (E01)
#endif
#if UNITY_EDITOR && !(ENABLE_UNITY_COLLECTIONS_CHECKS && ENABLE_PROFILER)
#error the Editor defines ENABLE_UNITY_COLLECTIONS_CHECKS and ENABLE_PROFILER (E04)
#endif
#if !UNITY_EDITOR && DEVELOPMENT_BUILD && !(ENABLE_UNITY_COLLECTIONS_CHECKS && ENABLE_PROFILER)
#error a development player defines ENABLE_UNITY_COLLECTIONS_CHECKS and ENABLE_PROFILER (E04)
#endif
#if !UNITY_EDITOR && !DEVELOPMENT_BUILD && (ENABLE_UNITY_COLLECTIONS_CHECKS || ENABLE_PROFILER)
#error a release player defines neither (E04)
#endif
#if UNITY_EDITOR && !(ENABLE_BURST_AOT && UNITY_TEAM_LICENSE)
#error Editor services symbols (E05)
#endif
#if !(ENABLE_PHYSICS && ENABLE_AUDIO && ENABLE_TERRAIN && ENABLE_TILEMAP && ENABLE_UNITYWEBREQUEST)
#error engine feature symbols (E06)
#endif
#if UNITY_EDITOR && !ENABLE_MONO
#error the Editor compiles with ENABLE_MONO whatever the platform's backend (D31)
#endif
#if UNITY_WEBGL && !UNITY_WEBGL_API
#error Web symbols (E10)
#endif
#if UNITY_EDITOR && !UNITY_INCLUDE_TESTS
#error the Editor compiles with UNITY_INCLUDE_TESTS when the test framework is resolved (D60)
#endif
namespace Example.Collections
{
    internal static class DefineProbe
    {
    }
}
