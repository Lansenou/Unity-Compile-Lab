// Fixture source for the ucl conformance corpus. Original code, Apache-2.0.

using NUnit.Framework;

namespace Example.Tools.Tests
{
    public class CalculatorTests
    {
        [Test]
        public void Adds() => Assert.AreEqual(3, Calculator.Add(1, 2));
    }
}
