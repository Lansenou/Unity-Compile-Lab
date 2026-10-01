using Ucl.Core.Rules;

namespace Ucl.Core.Tests;

public class DefineConstraintsTests
{
    private static readonly HashSet<string> Defined = ["A", "B", "UNITY_EDITOR"];

    private static bool Eval(params string[] constraints) => DefineConstraints.AreSatisfied(constraints, Defined.Contains);

    [Theory]
    [InlineData("A", true)]
    [InlineData("C", false)]
    [InlineData("!C", true)]
    [InlineData("!A", false)]
    [InlineData("! C", true)]
    [InlineData("C || A", true)]
    [InlineData("C||D", false)]
    [InlineData("C || !D", true)]
    [InlineData("!A || !B", false)]
    [InlineData("  ", true)]
    [InlineData("", true)]
    [InlineData("A-B", false)]
    [InlineData("A-B || A", true)]
    [InlineData("C ||", false)]
    [InlineData("!", false)]
    public void Single_constraint(string constraint, bool expected)
    {
        Assert.Equal(expected, DefineConstraints.IsSatisfied(constraint, Defined.Contains));
    }

    [Fact]
    public void All_entries_must_hold()
    {
        Assert.True(Eval());
        Assert.True(Eval("A", "B", "!C"));
        Assert.False(Eval("A", "C"));
        Assert.True(Eval("A", "", "C || UNITY_EDITOR"));
    }
}
