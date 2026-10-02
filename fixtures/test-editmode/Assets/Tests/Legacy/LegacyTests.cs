using Game.Runtime;
using NUnit.Framework;

namespace Game.Tests.Legacy
{
    public class LegacyTests
    {
        [Test]
        public void Legacy_test_assemblies_get_nunit()
        {
            var w = new Wallet();
            w.Earn(3);
            Assert.AreEqual(3, w.Coins);
        }
    }
}
