using NUnit.Framework;

namespace Game.Tests.Fixtures
{
    [SetUpFixture]
    public class NamespaceSetUp
    {
        public static int Runs;

        [OneTimeSetUp]
        public void Once() => Runs++;
    }

    public class UsesNamespaceSetUp
    {
        [Test]
        public void SetUp_fixture_ran_first() => Assert.AreEqual(1, NamespaceSetUp.Runs);
    }
}
