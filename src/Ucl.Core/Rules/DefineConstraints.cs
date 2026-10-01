namespace Ucl.Core.Rules;

/// <summary>Evaluates asmdef and plugin <c>defineConstraints</c>.</summary>
public static class DefineConstraints
{
    /// <summary>
    /// True when every constraint holds. A constraint is terms joined by <c>||</c>; a term is <c>SYMBOL</c> or
    /// <c>!SYMBOL</c>. Empty constraints are ignored; a malformed term is false, so the assembly is not compiled.
    /// </summary>
    public static bool AreSatisfied(IEnumerable<string> constraints, Func<string, bool> isDefined) =>
        constraints.All(c => IsSatisfied(c, isDefined));

    /// <summary>Evaluates one constraint.</summary>
    public static bool IsSatisfied(string constraint, Func<string, bool> isDefined)
    {
        if (string.IsNullOrWhiteSpace(constraint))
        {
            return true;
        }

        foreach (var raw in constraint.Split("||"))
        {
            var term = raw.Trim();
            var negated = term.StartsWith('!');
            var symbol = negated ? term[1..].Trim() : term;
            if (!DefineSet.IsValidSymbol(symbol))
            {
                continue;
            }

            if (isDefined(symbol) != negated)
            {
                return true;
            }
        }

        return false;
    }
}
