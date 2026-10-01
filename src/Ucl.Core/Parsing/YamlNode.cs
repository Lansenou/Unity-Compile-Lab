namespace Ucl.Core.Parsing;

/// <summary>A node of the small YAML subset Unity writes for <c>.meta</c> and <c>ProjectSettings.asset</c> files.</summary>
public sealed class YamlNode
{
    private YamlNode(string? scalar, IReadOnlyList<KeyValuePair<string, YamlNode>>? map, IReadOnlyList<YamlNode>? items)
    {
        Scalar = scalar;
        Map = map;
        Items = items;
    }

    /// <summary>Scalar text, or null for a mapping or sequence.</summary>
    public string? Scalar { get; }

    /// <summary>Mapping entries in file order, or null.</summary>
    public IReadOnlyList<KeyValuePair<string, YamlNode>>? Map { get; }

    /// <summary>Sequence items, or null.</summary>
    public IReadOnlyList<YamlNode>? Items { get; }

    /// <summary>Creates a scalar node.</summary>
    public static YamlNode OfScalar(string text) => new(text, null, null);

    /// <summary>Creates a mapping node.</summary>
    public static YamlNode OfMap(IReadOnlyList<KeyValuePair<string, YamlNode>> entries) => new(null, entries, null);

    /// <summary>Creates a sequence node.</summary>
    public static YamlNode OfItems(IReadOnlyList<YamlNode> items) => new(null, null, items);

    /// <summary>The value of the first entry named <paramref name="key"/>, or null.</summary>
    public YamlNode? this[string key] => Map?.FirstOrDefault(e => e.Key == key).Value;

    /// <summary>Scalar value of <paramref name="key"/>, or null.</summary>
    public string? Get(string key) => this[key]?.Scalar;
}
