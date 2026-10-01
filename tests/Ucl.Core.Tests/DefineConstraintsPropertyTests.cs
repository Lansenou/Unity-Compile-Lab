using Ucl.Core.Rules;

namespace Ucl.Core.Tests;

/// <summary>Random symbol sets and constraints compared with an independent evaluator.</summary>
public class DefineConstraintsPropertyTests
{
    private const int Cases = 800;
    private static readonly string[] Universe = ["A", "B", "C", "D", "UNITY_EDITOR", "UNITY_INCLUDE_TESTS", "MY_DEFINE_2", "_X"];

    private sealed record Term(string Symbol, bool Negated);

    private static List<List<Term>> RandomConstraints(Random rng)
    {
        var constraints = new List<List<Term>>();
        var count = rng.Next(0, 4);
        for (var i = 0; i < count; i++)
        {
            var terms = new List<Term>();
            var termCount = rng.Next(0, 4);
            for (var t = 0; t < termCount; t++)
            {
                terms.Add(new Term(Universe[rng.Next(Universe.Length)], rng.Next(2) == 0));
            }

            constraints.Add(terms);
        }

        return constraints;
    }

    private static string Render(List<Term> terms, Random rng)
    {
        string Space() => rng.Next(3) switch { 0 => string.Empty, 1 => " ", _ => "  " };
        return string.Join("||", terms.Select(t => Space() + (t.Negated ? "!" + (rng.Next(4) == 0 ? " " : string.Empty) : string.Empty) + t.Symbol + Space()));
    }

    // Independent evaluation: an empty constraint holds; otherwise some term must hold.
    private static bool Expected(List<List<Term>> constraints, HashSet<string> defined)
    {
        foreach (var terms in constraints)
        {
            if (terms.Count == 0)
            {
                continue;
            }

            var any = false;
            foreach (var t in terms)
            {
                if (defined.Contains(t.Symbol) ^ t.Negated)
                {
                    any = true;
                }
            }

            if (!any)
            {
                return false;
            }
        }

        return true;
    }

    [Fact]
    public void Matches_independent_evaluation()
    {
        var rng = new Random(3001);
        for (var n = 0; n < Cases; n++)
        {
            var defined = Universe.Where(_ => rng.Next(2) == 0).ToHashSet(StringComparer.Ordinal);
            var constraints = RandomConstraints(rng);
            var rendered = constraints.Select(c => Render(c, rng)).ToList();
            var actual = DefineConstraints.AreSatisfied(rendered, defined.Contains);
            Assert.True(Expected(constraints, defined) == actual, $"[{string.Join(" ; ", rendered)}] with {{{string.Join(",", defined)}}}");
        }
    }

    [Fact]
    public void Each_single_constraint_matches_independent_evaluation()
    {
        var rng = new Random(3002);
        for (var n = 0; n < Cases; n++)
        {
            var defined = Universe.Where(_ => rng.Next(2) == 0).ToHashSet(StringComparer.Ordinal);
            var terms = RandomConstraints(rng).FirstOrDefault() ?? [];
            var text = Render(terms, rng);
            Assert.Equal(Expected([terms], defined), DefineConstraints.IsSatisfied(text, defined.Contains));
        }
    }

    [Fact]
    public void Negation_flips_a_single_term()
    {
        var rng = new Random(3003);
        for (var n = 0; n < Cases; n++)
        {
            var defined = Universe.Where(_ => rng.Next(2) == 0).ToHashSet(StringComparer.Ordinal);
            var s = Universe[rng.Next(Universe.Length)];
            Assert.NotEqual(DefineConstraints.IsSatisfied(s, defined.Contains), DefineConstraints.IsSatisfied("!" + s, defined.Contains));
        }
    }
}
