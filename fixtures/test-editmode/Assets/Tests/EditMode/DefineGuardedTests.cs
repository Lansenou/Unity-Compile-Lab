using NUnit.Framework;

namespace Game.Tests
{
    // Cases behind Unity's defines: compiled exactly when the Editor compiles them.
    public class DefineGuardedTests
    {
#if UNITY_5_3_OR_NEWER
        [Test]
        public void Historical_version_symbol_is_defined() => Assert.Pass();

        [TestCase("a")]
        [TestCase("b")]
        public void Parameterised_case_behind_a_version_symbol(string value) => Assert.IsNotEmpty(value);
#endif

#if UNITY_EDITOR
        [Test]
        public void Editor_symbol_is_defined() => Assert.Pass();
#else
        [Test]
        public void Never_compiled_in_the_Editor() => Assert.Fail("the Editor never compiles this branch");
#endif

#if NET_UNITY_4_8
        [Test]
        public void Editor_assemblies_use_the_net_framework_profile() => Assert.Pass();
#endif
    }
}
