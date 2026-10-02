// Fixture source for the ucl conformance corpus. Original code, Apache-2.0.

public static class WebKeyboard
{
    public static void Release()
    {
#if UNITY_WEBGL
        // UnityEngine.WebGLModule.dll lives in the Web build support folder, not in Managed/UnityEngine.
        UnityEngine.WebGLInput.captureAllKeyboardInput = false;
#endif
    }
}
