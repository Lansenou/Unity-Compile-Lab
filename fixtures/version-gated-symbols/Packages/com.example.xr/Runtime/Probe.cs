// Fixture source for the ucl conformance corpus. Original code, Apache-2.0.
// docs/defines.md D53 (resource Unity: the a/b/f/p suffix is ignored) and E08, E17. A broken rule stops the compile.
#if !UNITY_2022_2_14F1_OR_NEWER
#error versionDefines Unity 2022.2.14f1 holds on every Unity 6 editor (D53)
#endif
#if EXAMPLE_BEFORE_6000_3
#error [6000.0.0a1,6000.3.0b1) does not hold on 6000.3 (D53)
#endif
#if !EXAMPLE_XR_SUPPORTED
#error package version 2.0.0-pre.3 is in [2.0.0-pre.1,3.0.0) (D53)
#endif
#if EXAMPLE_AT_6000_3_19 && ENABLE_AUDIO_SCRIPTABLE_PIPELINE
#error 6000.3.19 does not define ENABLE_AUDIO_SCRIPTABLE_PIPELINE (E08)
#endif
#if !EXAMPLE_AT_6000_3_19 && !ENABLE_AUDIO_SCRIPTABLE_PIPELINE
#error 6000.3 before .19 defines ENABLE_AUDIO_SCRIPTABLE_PIPELINE (E08)
#endif
#if UNITY_EDITOR && EXAMPLE_AT_6000_3_19 && !ENABLE_PROFILER_ASSISTANT_INTEGRATION
#error the 6000.3.19 Editor defines ENABLE_PROFILER_ASSISTANT_INTEGRATION (E17)
#endif
#if !EXAMPLE_AT_6000_3_19 && ENABLE_PROFILER_ASSISTANT_INTEGRATION
#error ENABLE_PROFILER_ASSISTANT_INTEGRATION starts at 6000.3.19 (E17)
#endif
namespace Example.XR
{
    public static class Probe
    {
    }
}
