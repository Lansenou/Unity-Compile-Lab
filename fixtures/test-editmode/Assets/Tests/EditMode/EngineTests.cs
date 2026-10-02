using Game.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    public class EngineTests
    {
        [Test]
        public void Vector_math_is_managed() => Assert.AreEqual(new Vector3(1f, 1f, 1f), Spawner.Midpoint(Vector3.zero, new Vector3(2f, 2f, 2f)));

        [Test]
        public void Spawning_needs_the_engine() => Assert.IsNotNull(Spawner.Spawn("enemy"));

        [Test]
        public void Logging_needs_the_engine() => Debug.Log("hello");
    }
}
