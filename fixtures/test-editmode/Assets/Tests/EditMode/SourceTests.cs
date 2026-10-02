using System.Collections.Generic;
using NUnit.Framework;

namespace Game.Tests
{
    public class SourceTests
    {
        private static IEnumerable<TestCaseData> Prices()
        {
            yield return new TestCaseData(1, 2).Returns(3);
            yield return new TestCaseData(2, 2).Returns(4);
            yield return new TestCaseData(0, 0).Returns(0);
            yield return new TestCaseData(-1, 1).Returns(0);
        }

        [TestCaseSource(nameof(Prices))]
        public int Prices_add_up(int a, int b) => a + b;
    }
}
