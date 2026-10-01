using Ucl.Core.Parsing;

namespace Ucl.Core.Tests;

public class SimpleYamlTests
{
    [Fact]
    public void Skips_directives_document_markers_and_comments_and_merges_documents()
    {
        var root = SimpleYaml.Parse("%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!129 &1\n# comment\nFirst:\n  a: 1\n--- !u!1 &2\nSecond:\n  b: 2\n");
        Assert.Equal(["First", "Second"], root.Map!.Select(e => e.Key));
        Assert.Equal("1", root["First"]!.Get("a"));
        Assert.Equal("2", root["Second"]!.Get("b"));
    }

    [Fact]
    public void Scalars_quoted_and_plain()
    {
        var root = SimpleYaml.Parse("a: plain text\nb: \"double: quoted\"\nc: 'single'\nd: x\r\ne:\n");
        Assert.Equal("plain text", root.Get("a"));
        Assert.Equal("double: quoted", root.Get("b"));
        Assert.Equal("single", root.Get("c"));
        Assert.Equal("x", root.Get("d"));
        Assert.Equal(string.Empty, root.Get("e"));
        Assert.Null(root.Get("missing"));
        Assert.Null(root["missing"]);
    }

    [Fact]
    public void Empty_flow_collections()
    {
        var root = SimpleYaml.Parse("m: {}\ns: []\nf: {fileID: 0}\n");
        Assert.NotNull(root["m"]!.Map);
        Assert.Empty(root["m"]!.Map!);
        Assert.Null(root["m"]!.Scalar);
        Assert.NotNull(root["s"]!.Items);
        Assert.Empty(root["s"]!.Items!);
        Assert.Equal("{fileID: 0}", root.Get("f"));
    }

    [Fact]
    public void Indented_and_unindented_sequences()
    {
        var root = SimpleYaml.Parse("top:\n  list:\n  - a\n  - 'b'\n  other:\n    - c\n    - d\nlabels:\n- RoslynAnalyzer\n- Other\n");
        Assert.Equal(["a", "b"], root["top"]!["list"]!.Items!.Select(i => i.Scalar));
        Assert.Equal(["c", "d"], root["top"]!["other"]!.Items!.Select(i => i.Scalar));
        Assert.Equal(["RoslynAnalyzer", "Other"], root["labels"]!.Items!.Select(i => i.Scalar));
    }

    [Fact]
    public void Sequence_of_mappings_and_nested_items()
    {
        var text = "data:\n- first:\n    : Any\n  second:\n    enabled: 1\n- key: v\n  other: w\n-\n  nested: 1\n-\n- last\n";
        var items = SimpleYaml.Parse(text)["data"]!.Items!;
        Assert.Equal(5, items.Count);
        Assert.Equal("Any", items[0]["first"]!.Map![0].Value.Scalar);
        Assert.Equal(string.Empty, items[0]["first"]!.Map![0].Key);
        Assert.Equal("1", items[0]["second"]!.Get("enabled"));
        Assert.Equal("v", items[1].Get("key"));
        Assert.Equal("w", items[1].Get("other"));
        Assert.Equal("1", items[2].Get("nested"));
        Assert.Equal(string.Empty, items[3].Scalar);
        Assert.Equal("last", items[4].Scalar);
    }

    [Fact]
    public void Quoted_keys()
    {
        var root = SimpleYaml.Parse("\"quoted key\": 1\n'single key': 2\n\"broken\n");
        Assert.Equal("1", root.Get("quoted key"));
        Assert.Equal("2", root.Get("single key"));
        Assert.Equal(2, root.Map!.Count);
    }

    [Fact]
    public void Lines_without_keys_are_skipped()
    {
        var root = SimpleYaml.Parse("a: 1\njust text\n{flow: map}\n[flow, list]\nb: 2\n");
        Assert.Equal("1", root.Get("a"));
        Assert.Equal("2", root.Get("b"));
    }

    [Fact]
    public void Orphan_deeper_lines_are_skipped()
    {
        var root = SimpleYaml.Parse("junk\n    deeper: 1\n    deeper2: 2\na: 3\n");
        Assert.Equal("3", root.Get("a"));
        Assert.Null(root["deeper"]);
        Assert.Null(root["deeper2"]);

        var nested = SimpleYaml.Parse("    orphan:\n        deeper: 2\n      deeper2: 3\na: 3\n");
        Assert.Equal("2", nested["orphan"]?.Get("deeper"));
        Assert.Null(nested["orphan"]?["deeper2"]);
        Assert.Equal("3", nested.Get("a"));
    }

    [Fact]
    public void Deeper_lines_after_a_value_continue_it()
    {
        // Unity wraps long flow mappings and scalars onto deeper-indented lines (ProjectSettings.asset).
        var root = SimpleYaml.Parse(
            "PlayerSettings:\n  icon: {fileID: 2800000, guid: 0a1b,\n    type: 3}\n  text: one\n    two\n      three\n"
            + "  list:\n  - item: a\n      wrapped\n    other: b\n  - plain\n     cont\n  after: 1\n");
        var player = root["PlayerSettings"]!;
        Assert.Equal("{fileID: 2800000, guid: 0a1b, type: 3}", player.Get("icon"));
        Assert.Equal("one two three", player.Get("text"));
        Assert.Equal("a wrapped", player["list"]!.Items![0].Get("item"));
        Assert.Equal("b", player["list"]!.Items![0].Get("other"));
        Assert.Equal("plain cont", player["list"]!.Items![1].Scalar);
        Assert.Equal("1", player.Get("after"));
    }

    [Fact]
    public void Top_level_sequence_yields_empty_mapping()
    {
        var root = SimpleYaml.Parse("- a\n- b\n");
        Assert.NotNull(root.Map);
        Assert.Empty(root.Map!);
    }

    [Fact]
    public void Empty_text()
    {
        Assert.Empty(SimpleYaml.Parse(string.Empty).Map!);
        Assert.Empty(SimpleYaml.Parse("\n\n# only comments\n").Map!);
    }

    [Fact]
    public void First_entry_wins_for_duplicate_keys()
    {
        Assert.Equal("1", SimpleYaml.Parse("a: 1\na: 2\n").Get("a"));
    }

    [Fact]
    public void YamlNode_factories()
    {
        var scalar = YamlNode.OfScalar("x");
        Assert.Equal("x", scalar.Scalar);
        Assert.Null(scalar.Map);
        Assert.Null(scalar.Items);
        Assert.Null(scalar["k"]);
        Assert.Null(scalar.Get("k"));
        var items = YamlNode.OfItems([scalar]);
        Assert.Single(items.Items!);
        var map = YamlNode.OfMap([new("k", scalar)]);
        Assert.Same(scalar, map["k"]);
    }
}
