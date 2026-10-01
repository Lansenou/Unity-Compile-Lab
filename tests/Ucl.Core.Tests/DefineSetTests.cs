using Ucl.Core.Rules;

namespace Ucl.Core.Tests;

public class DefineSetTests
{
    [Fact]
    public void Symbols_are_ordinal_sorted_and_first_reason_wins()
    {
        var d = new DefineSet();
        d.Add("b", "r1");
        d.Add("B", "r2");
        d.Add("a", "r3");
        d.Add("b", "later");
        Assert.Equal(["B", "a", "b"], d.Symbols);
        Assert.Equal("r1", d.Reasons["b"]);
        Assert.True(d.Contains("a"));
        Assert.False(d.Contains("A"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("1ABC")]
    [InlineData("A-B")]
    [InlineData("A B")]
    [InlineData("A;B")]
    [InlineData("!A")]
    public void Invalid_symbols_are_ignored(string symbol)
    {
        var d = new DefineSet();
        d.Add(symbol, "r");
        Assert.Empty(d.Symbols);
        Assert.False(DefineSet.IsValidSymbol(symbol));
    }

    [Theory]
    [InlineData("A")]
    [InlineData("_A1")]
    [InlineData("UNITY_6000_0_OR_NEWER")]
    [InlineData("ÉTÉ")]
    public void Valid_symbols(string symbol) => Assert.True(DefineSet.IsValidSymbol(symbol));

    [Fact]
    public void Copy_is_independent()
    {
        var d = new DefineSet();
        d.Add("A", "r");
        var c = d.Copy();
        c.Add("B", "r");
        Assert.Equal(["A"], d.Symbols);
        Assert.Equal(["A", "B"], c.Symbols);
        Assert.Equal("r", c.Reasons["A"]);
    }
}
