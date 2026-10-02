using NUnit.Framework;

namespace Game.Tests
{
    public class OutcomeTests
    {
        [Test]
        public void Arithmetic_is_wrong_on_purpose() => Assert.AreEqual(4, 1 + 2, "a real failure, not an engine call");

        [Test]
        [Ignore("not written yet")]
        public void Ignored_case() => Assert.Fail();

        [Test]
        public void Inconclusive_case() => Assume.That(false, "skipped at run time");

        [Test]
        [Explicit("runs only when selected")]
        public void Explicit_case() => Assert.Pass();
    }
}
