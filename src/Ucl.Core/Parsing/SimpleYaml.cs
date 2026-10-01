namespace Ucl.Core.Parsing;

/// <summary>
/// Parses the block-style YAML subset Unity serialises: mappings, sequences (including Unity's
/// unindented <c>key:\n- item</c> form), plain or quoted scalars, and empty flow collections.
/// Other flow collections are kept as scalar text. Directives and document markers are skipped and the
/// top-level mappings of all documents are merged.
/// </summary>
public static class SimpleYaml
{
    /// <summary>Parses <paramref name="text"/> into a mapping node (empty when the text has no mapping).</summary>
    public static YamlNode Parse(string text)
    {
        var lines = new List<(int Indent, string Content)>();

        // Column of the last line's key (or item) when that line carried a scalar value, else -1. Unity wraps long
        // values (flow mappings such as "{fileID: 0, guid: ...,\n    type: 3}", long strings) onto deeper-indented
        // continuation lines; a deeper line after a line that already has a value can only be such a continuation.
        var valueColumn = -1;
        foreach (var raw in text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n'))
        {
            var trimmed = raw.TrimEnd();
            var content = trimmed.TrimStart(' ');
            if (content.Length == 0 || content[0] == '#' || content[0] == '%' || content.StartsWith("---", StringComparison.Ordinal))
            {
                continue;
            }

            var indent = trimmed.Length - content.Length;
            if (valueColumn >= 0 && indent > valueColumn && lines.Count > 0)
            {
                lines[^1] = (lines[^1].Indent, lines[^1].Content + " " + content);
                continue;
            }

            lines.Add((indent, content));
            valueColumn = ValueColumn(indent, content);
        }

        var entries = new List<KeyValuePair<string, YamlNode>>();
        var i = 0;
        while (i < lines.Count)
        {
            var before = i;
            var node = ParseBlock(lines, ref i, lines[i].Indent);
            if (node.Map is not null)
            {
                entries.AddRange(node.Map);
            }

            if (i == before)
            {
                i++;
            }
        }

        return YamlNode.OfMap(entries);
    }

    private static YamlNode ParseBlock(List<(int Indent, string Content)> lines, ref int i, int indent)
    {
        return IsItem(lines[i].Content) ? ParseSequence(lines, ref i, indent) : ParseMap(lines, ref i, indent);
    }

    private static bool IsItem(string content) => content == "-" || content.StartsWith("- ", StringComparison.Ordinal);

    // The column a continuation line must be deeper than, or -1 when the line opens a block (no scalar value).
    private static int ValueColumn(int indent, string content)
    {
        while (IsItem(content))
        {
            var rest = content.Length > 1 ? content[2..].TrimStart() : string.Empty;
            indent += content.Length - rest.Length;
            content = rest;
            if (content.Length == 0)
            {
                return -1;
            }
        }

        var split = SplitKey(content);
        return split is null || split.Value.Value.Length > 0 ? indent : -1;
    }

    private static YamlNode ParseSequence(List<(int Indent, string Content)> lines, ref int i, int indent)
    {
        var items = new List<YamlNode>();
        while (i < lines.Count && lines[i].Indent == indent && IsItem(lines[i].Content))
        {
            var rest = lines[i].Content.Length > 1 ? lines[i].Content[2..].TrimStart() : string.Empty;
            if (rest.Length == 0)
            {
                i++;
                items.Add(i < lines.Count && lines[i].Indent > indent ? ParseBlock(lines, ref i, lines[i].Indent) : YamlNode.OfScalar(string.Empty));
            }
            else if (SplitKey(rest) is not null)
            {
                // "- key: value" starts a mapping whose first entry sits on the item line.
                var childIndent = indent + (lines[i].Content.Length - rest.Length);
                lines[i] = (childIndent, rest);
                items.Add(ParseMap(lines, ref i, childIndent));
            }
            else
            {
                items.Add(YamlNode.OfScalar(Unquote(rest)));
                i++;
            }
        }

        return YamlNode.OfItems(items);
    }

    private static YamlNode ParseMap(List<(int Indent, string Content)> lines, ref int i, int indent)
    {
        var entries = new List<KeyValuePair<string, YamlNode>>();
        while (i < lines.Count && lines[i].Indent == indent && !IsItem(lines[i].Content))
        {
            var split = SplitKey(lines[i].Content);
            if (split is null)
            {
                i++;
                continue;
            }

            var (key, value) = split.Value;
            i++;
            YamlNode node;
            if (value.Length > 0)
            {
                node = YamlNode.OfScalar(Unquote(value));
                if (value == "{}") node = YamlNode.OfMap([]);
                else if (value == "[]") node = YamlNode.OfItems([]);
            }
            else if (i < lines.Count && lines[i].Indent > indent)
            {
                node = ParseBlock(lines, ref i, lines[i].Indent);
            }
            else if (i < lines.Count && lines[i].Indent == indent && IsItem(lines[i].Content))
            {
                node = ParseSequence(lines, ref i, indent);
            }
            else
            {
                node = YamlNode.OfScalar(string.Empty);
            }

            entries.Add(new(key, node));
        }

        // Lines indented deeper than expected without a parent key are skipped rather than misread.
        while (i < lines.Count && lines[i].Indent > indent && entries.Count == 0)
        {
            i++;
        }

        return YamlNode.OfMap(entries);
    }

    private static (string Key, string Value)? SplitKey(string content)
    {
        if (content.StartsWith('"') || content.StartsWith('\''))
        {
            var q = content[0];
            var end = content.IndexOf(q, 1);
            if (end > 0 && end + 1 < content.Length && content[end + 1] == ':')
            {
                return (content[1..end], content[(end + 2)..].Trim());
            }

            return null;
        }

        if (content.StartsWith('{') || content.StartsWith('['))
        {
            return null;
        }

        var idx = content.IndexOf(": ", StringComparison.Ordinal);
        if (idx >= 0)
        {
            return (content[..idx].Trim(), content[(idx + 2)..].Trim());
        }

        return content.EndsWith(':') ? (content[..^1].Trim(), string.Empty) : null;
    }

    private static string Unquote(string value)
    {
        if (value.Length >= 2 && ((value[0] == '"' && value[^1] == '"') || (value[0] == '\'' && value[^1] == '\'')))
        {
            return value[1..^1];
        }

        return value;
    }
}
