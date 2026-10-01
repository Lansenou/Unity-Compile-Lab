namespace Ucl.Core.Rules;

/// <summary>An ordered set of preprocessor symbols, each with the reason it is defined (a define-table row id or a file).</summary>
public sealed class DefineSet
{
    private readonly SortedDictionary<string, string> _reasons;

    /// <summary>Creates an empty set.</summary>
    public DefineSet()
    {
        _reasons = new SortedDictionary<string, string>(StringComparer.Ordinal);
    }

    private DefineSet(SortedDictionary<string, string> reasons)
    {
        _reasons = new SortedDictionary<string, string>(reasons, StringComparer.Ordinal);
    }

    /// <summary>Symbols in ordinal order.</summary>
    public IReadOnlyCollection<string> Symbols => _reasons.Keys;

    /// <summary>Symbol to reason, in ordinal order.</summary>
    public IReadOnlyDictionary<string, string> Reasons => _reasons;

    /// <summary>True when <paramref name="symbol"/> is defined.</summary>
    public bool Contains(string symbol) => _reasons.ContainsKey(symbol);

    /// <summary>Adds a symbol; the first reason given for a symbol is kept.</summary>
    public void Add(string symbol, string reason)
    {
        if (IsValidSymbol(symbol))
        {
            _reasons.TryAdd(symbol, reason);
        }
    }

    /// <summary>Removes every symbol whose reason is <paramref name="reason"/>.</summary>
    public void RemoveWithReason(string reason)
    {
        foreach (var symbol in _reasons.Where(r => r.Value == reason).Select(r => r.Key).ToList())
        {
            _reasons.Remove(symbol);
        }
    }

    /// <summary>Returns a copy that can be extended without changing this set.</summary>
    public DefineSet Copy() => new(_reasons);

    /// <summary>True for a valid C# conditional symbol (identifier characters, not starting with a digit).</summary>
    public static bool IsValidSymbol(string symbol) =>
        symbol.Length > 0 && !char.IsAsciiDigit(symbol[0]) && symbol.All(c => char.IsLetterOrDigit(c) || c == '_');
}
