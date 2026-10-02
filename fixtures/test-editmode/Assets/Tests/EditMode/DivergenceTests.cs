using System.Globalization;
using NUnit.Framework;

namespace Game.Tests.Divergence
{
    // Outcomes that differ between CoreCLR (ucl test) and Mono (the Unity Editor); docs/test.md, "Divergences".
    // Both pass under ucl test and fail in the Editor, whose Mono formats with 15 (double) and 7 (float) digits.
    public class DivergenceTests
    {
        [Test]
        public void Double_ToString_is_shortest_round_trip() =>
            Assert.AreEqual("0.30000000000000004", (0.1 + 0.2).ToString(CultureInfo.InvariantCulture));

        [Test]
        public void Float_ToString_is_shortest_round_trip() =>
            Assert.AreEqual("0.33333334", (1f / 3f).ToString(CultureInfo.InvariantCulture));
    }
}
