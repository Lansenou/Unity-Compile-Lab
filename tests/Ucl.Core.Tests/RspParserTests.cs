using Ucl.Core.Parsing;

namespace Ucl.Core.Tests;

public class RspParserTests
{
    [Fact]
    public void Tokenize_splits_on_whitespace_and_keeps_quoted_groups()
    {
        var tokens = RspParser.Tokenize("-define:A;B   -nowarn:0618\n\t-r:\"Assets/My Libs/x.dll\" \"-additionalfile:Assets/a b.txt\"\r\n");
        Assert.Equal(["-define:A;B", "-nowarn:0618", "-r:\"Assets/My Libs/x.dll\"", "\"-additionalfile:Assets/a b.txt\""], tokens);
    }

    [Fact]
    public void Tokenize_skips_comment_and_blank_lines()
    {
        var tokens = RspParser.Tokenize("# a comment -define:NO\n\n   # indented comment\n-define:YES # not a comment here\n");
        Assert.Equal(["-define:YES", "#", "not", "a", "comment", "here"], tokens);
    }

    [Fact]
    public void Tokenize_empty() => Assert.Empty(RspParser.Tokenize(string.Empty));

    [Fact]
    public void Defines_in_all_spellings()
    {
        var o = RspParser.Parse("-define:A;B\n/define:C,D\n-d:E\n/D:F\n-DEFINE:\"G;H\"\n-define:A");
        Assert.Equal(["A", "B", "C", "D", "E", "F", "G", "H", "A"], o.Defines);
        Assert.Empty(o.Unsupported);
    }

    [Fact]
    public void Nowarn_normalises_numeric_ids()
    {
        var o = RspParser.Parse("-nowarn:618,0649;CS0169 -nowarn:IDE0051 /nowarn:0");
        Assert.Equal(["CS0618", "CS0649", "CS0169", "IDE0051", "CS0000"], o.NoWarn);
    }

    [Theory]
    [InlineData("618", "CS0618")]
    [InlineData("0618", "CS0618")]
    [InlineData("12345", "CS12345")]
    [InlineData("CS0618", "CS0618")]
    [InlineData("UNT0001", "UNT0001")]
    public void Ids(string id, string expected) => Assert.Equal([expected], RspParser.Ids(id));

    [Fact]
    public void Warnaserror_forms()
    {
        Assert.True(RspParser.Parse("-warnaserror").WarnAsErrorAll);
        Assert.True(RspParser.Parse("-warnaserror+").WarnAsErrorAll);
        Assert.False(RspParser.Parse("-warnaserror-").WarnAsErrorAll);
        Assert.False(RspParser.Parse("-warnaserror -warnaserror-").WarnAsErrorAll);
        Assert.Null(RspParser.Parse(string.Empty).WarnAsErrorAll);

        var ids = RspParser.Parse("-warnaserror:618,CS0168 -warnaserror+:0649 -warnaserror-:CS0414;0219");
        Assert.Null(ids.WarnAsErrorAll);
        Assert.Equal(["CS0618", "CS0168", "CS0649"], ids.WarnAsErrorIds);
        Assert.Equal(["CS0414", "CS0219"], ids.WarnNotAsErrorIds);
    }

    [Fact]
    public void Single_valued_options_last_wins()
    {
        var o = RspParser.Parse("-langversion:8.0 -langversion:latest -nullable:warnings -ruleset:Assets/A.ruleset /ruleset:Assets/B.ruleset");
        Assert.Equal("latest", o.LangVersion);
        Assert.Equal("warnings", o.Nullable);
        Assert.Equal("Assets/B.ruleset", o.RuleSet);
    }

    [Theory]
    [InlineData("-nullable", "enable")]
    [InlineData("-nullable+", "enable")]
    [InlineData("-nullable-", "disable")]
    [InlineData("-nullable:annotations", "annotations")]
    public void Nullable_forms(string arg, string expected) => Assert.Equal(expected, RspParser.Parse(arg).Nullable);

    [Theory]
    [InlineData("-unsafe", true)]
    [InlineData("/unsafe+", true)]
    [InlineData("-unsafe-", false)]
    [InlineData("-define:X", null)]
    public void Unsafe_forms(string arg, bool? expected) => Assert.Equal(expected, RspParser.Parse(arg).Unsafe);

    [Fact]
    public void Paths_lists()
    {
        var o = RspParser.Parse("-additionalfile:Assets/a.txt;Assets/b.txt -analyzerconfig:Assets/.globalconfig -r:Assets/Libs/x.dll,Assets/Libs/y.dll -reference:Assets/z.dll");
        Assert.Equal(["Assets/a.txt", "Assets/b.txt"], o.AdditionalFiles);
        Assert.Equal(["Assets/.globalconfig"], o.AnalyzerConfigs);
        Assert.Equal(["Assets/Libs/x.dll", "Assets/Libs/y.dll", "Assets/z.dll"], o.References);
    }

    [Fact]
    public void Quoted_whole_argument_is_unwrapped()
    {
        var o = RspParser.Parse("\"-r:Assets/My Libs/x.dll\" -r:\"Assets/Other Libs/y.dll\"");
        Assert.Equal(["Assets/My Libs/x.dll", "Assets/Other Libs/y.dll"], o.References);
    }

    [Fact]
    public void Unsupported_options_are_collected()
    {
        var o = RspParser.Parse("-optimize+ -debug:portable x -nowarn -define -langversion -r -additionalfile -analyzerconfig -ruleset - /");
        Assert.Equal(["-optimize+", "-debug:portable", "x", "-nowarn", "-define", "-langversion", "-r", "-additionalfile", "-analyzerconfig", "-ruleset", "-", "/"], o.Unsupported);
    }

    [Fact]
    public void Parse_args_overload()
    {
        var o = RspParser.Parse(["-define:X", "-unsafe"]);
        Assert.Equal(["X"], o.Defines);
        Assert.True(o.Unsafe);
    }

    [Fact]
    public void Then_accumulates_lists_and_later_single_values_win()
    {
        var first = RspParser.Parse("-define:A -nowarn:1 -warnaserror -warnaserror:2 -warnaserror-:3 -langversion:8 -nullable:enable -unsafe -additionalfile:a -analyzerconfig:c1 -ruleset:r1 -r:x.dll -bogus");
        var second = RspParser.Parse("-define:B -nowarn:4 -warnaserror:5 -warnaserror-:6 -additionalfile:b -analyzerconfig:c2 -r:y.dll -bogus2");
        var merged = first.Then(second);
        Assert.Equal(["A", "B"], merged.Defines);
        Assert.Equal(["CS0001", "CS0004"], merged.NoWarn);
        Assert.True(merged.WarnAsErrorAll);
        Assert.Equal(["CS0002", "CS0005"], merged.WarnAsErrorIds);
        Assert.Equal(["CS0003", "CS0006"], merged.WarnNotAsErrorIds);
        Assert.Equal("8", merged.LangVersion);
        Assert.Equal("enable", merged.Nullable);
        Assert.True(merged.Unsafe);
        Assert.Equal(["a", "b"], merged.AdditionalFiles);
        Assert.Equal(["c1", "c2"], merged.AnalyzerConfigs);
        Assert.Equal("r1", merged.RuleSet);
        Assert.Equal(["x.dll", "y.dll"], merged.References);
        Assert.Equal(["-bogus", "-bogus2"], merged.Unsupported);

        var third = merged.Then(RspParser.Parse("-warnaserror- -langversion:9.0 -nullable:disable -unsafe- -ruleset:r2"));
        Assert.False(third.WarnAsErrorAll);
        Assert.Equal("9.0", third.LangVersion);
        Assert.Equal("disable", third.Nullable);
        Assert.False(third.Unsafe);
        Assert.Equal("r2", third.RuleSet);
    }

    [Fact]
    public void Empty_options()
    {
        var e = RspOptions.Empty;
        Assert.Empty(e.Defines);
        Assert.Null(e.LangVersion);
        var m = e.Then(RspOptions.Empty);
        Assert.Empty(m.Defines);
        Assert.Empty(m.Unsupported);
        Assert.Null(m.WarnAsErrorAll);
        Assert.Null(m.RuleSet);
    }
}
