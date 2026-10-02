// Original stand-in for part of the Unity Test Framework, written for the ucl fixtures. Not Unity code. Apache-2.0.
using System;
using NUnit.Framework;

namespace UnityEngine.TestTools
{
    /// <summary>A test that runs as a coroutine in the Editor or a player: it needs the engine loop.</summary>
    [AttributeUsage(AttributeTargets.Method)]
    public class UnityTestAttribute : TestAttribute
    {
    }

    /// <summary>Runs a test only on the listed platforms.</summary>
    [AttributeUsage(AttributeTargets.Assembly | AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
    public class UnityPlatformAttribute : Attribute
    {
        public UnityPlatformAttribute() { }

        public UnityPlatformAttribute(params RuntimePlatform[] include) { }

        public RuntimePlatform[] include { get; set; }

        public RuntimePlatform[] exclude { get; set; }
    }

    /// <summary>Marks a test that needs Play Mode.</summary>
    [AttributeUsage(AttributeTargets.Assembly | AttributeTargets.Class | AttributeTargets.Method)]
    public class RequiresPlayModeAttribute : Attribute
    {
        public RequiresPlayModeAttribute(bool shouldRunInPlayMode = true) { }
    }

    /// <summary>Expectations on the messages the test writes to the Unity log.</summary>
    public static class LogAssert
    {
        public static bool ignoreFailingMessages { get; set; }

        public static void Expect(LogType type, string message) => throw new InvalidOperationException("LogAssert needs the Unity log");

        public static void NoUnexpectedReceived() => throw new InvalidOperationException("LogAssert needs the Unity log");
    }
}
