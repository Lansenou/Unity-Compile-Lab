// Original stand-in for part of the Unity API, written for the ucl fixtures. Not Unity code. Apache-2.0.
using System.Runtime.CompilerServices;

namespace UnityEditor
{
    /// <summary>
    /// Members that the real engine implements in native code call this. Like a real engine binding outside the
    /// Editor, the call fails in the runtime itself: CoreCLR refuses an InternalCall in a non-system module with
    /// System.Security.SecurityException ("ECall methods must be packaged into a system module"), Mono with
    /// MissingMethodException. ucl test classifies both as needs-unity (docs/test.md).
    /// </summary>
    internal static class Native
    {
        [MethodImpl(MethodImplOptions.InternalCall)]
        public static extern System.Exception Unavailable();
    }
}
