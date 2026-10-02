using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Tests
{
    public class UnityOnlyTests
    {
        [UnityTest]
        public IEnumerator Waits_a_frame()
        {
            yield return null;
        }

        [Test]
        public void Expects_a_log_message()
        {
            LogAssert.Expect(LogType.Log, "hello");
            Debug.Log("hello");
        }

        [Test]
        [UnityPlatform(RuntimePlatform.WindowsEditor)]
        public void Windows_editor_only() => Assert.Pass();

        [Test]
        [RequiresPlayMode]
        public void Needs_play_mode() => Assert.Pass();
    }
}
