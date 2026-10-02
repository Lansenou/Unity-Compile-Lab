// Fixture source for the ucl conformance corpus. Original code, Apache-2.0.

using NUnit.Framework;

namespace Example.Input.TestFramework
{
    public class InputTestFixture
    {
        [SetUp]
        public virtual void Setup() => InputState.Frame = 0;
    }
}
