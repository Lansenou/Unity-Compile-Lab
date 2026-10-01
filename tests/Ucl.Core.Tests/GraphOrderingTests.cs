using Ucl.Core.Graph;

namespace Ucl.Core.Tests;

public class GraphOrderingTests
{
    private static Dictionary<string, IReadOnlyList<string>> Edges(params (string Node, string[] Deps)[] edges) =>
        edges.ToDictionary(e => e.Node, e => (IReadOnlyList<string>)e.Deps, StringComparer.Ordinal);

    [Fact]
    public void Acyclic_graph_has_no_cycles()
    {
        var e = Edges(("A", ["B", "C"]), ("B", ["C"]), ("C", []));
        Assert.Empty(GraphOrdering.FindCycles(e));
    }

    [Fact]
    public void Two_node_cycle_and_bystanders()
    {
        var e = Edges(("A", ["B"]), ("B", ["A"]), ("C", ["A"]), ("D", []));
        Assert.Equal(["A", "B"], GraphOrdering.FindCycles(e).Order(StringComparer.Ordinal));
    }

    [Fact]
    public void Self_loop_is_a_cycle()
    {
        var e = Edges(("A", ["A"]), ("B", []));
        Assert.Equal(["A"], GraphOrdering.FindCycles(e));
    }

    [Fact]
    public void Three_node_cycle_and_separate_two_node_cycle()
    {
        var e = Edges(("A", ["B"]), ("B", ["C"]), ("C", ["A"]), ("X", ["Y"]), ("Y", ["X", "A"]));
        Assert.Equal(["A", "B", "C", "X", "Y"], GraphOrdering.FindCycles(e).Order(StringComparer.Ordinal));
    }

    [Fact]
    public void Edges_to_unknown_nodes_are_ignored()
    {
        var e = Edges(("A", ["Missing", "B"]), ("B", ["Missing"]));
        Assert.Empty(GraphOrdering.FindCycles(e));
        Assert.Equal(["B", "A"], GraphOrdering.TopologicalOrder(e));
    }

    [Fact]
    public void Topological_order_puts_dependencies_first_with_ordinal_ties()
    {
        var e = Edges(("Z", []), ("A", ["Z"]), ("M", []), ("B", ["A", "M"]), ("a", []));
        Assert.Equal(["M", "Z", "A", "B", "a"], GraphOrdering.TopologicalOrder(e));
    }

    [Fact]
    public void Diamond()
    {
        var e = Edges(("Top", ["L", "R"]), ("L", ["Base"]), ("R", ["Base"]), ("Base", []));
        Assert.Equal(["Base", "L", "R", "Top"], GraphOrdering.TopologicalOrder(e));
    }

    [Fact]
    public void Empty_graph()
    {
        Assert.Empty(GraphOrdering.TopologicalOrder(Edges()));
        Assert.Empty(GraphOrdering.FindCycles(Edges()));
    }
}
