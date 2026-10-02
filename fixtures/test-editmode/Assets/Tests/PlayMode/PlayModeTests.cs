using System.Collections;
using Game.Runtime;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Game.Tests.PlayMode
{
    // A Play Mode assembly (not Editor-only): the Unity Test Framework runs it in Play Mode only.
    public class PlayModeTests
    {
        [Test]
        public void Pure_but_in_a_play_mode_assembly() => Assert.AreEqual(0, new Wallet().Coins);

        [UnityTest]
        public IEnumerator Waits_in_play_mode()
        {
            yield return null;
        }
    }
}
