namespace Ucl.Core.Parsing;

/// <summary>Parses Unity <c>.meta</c> files.</summary>
public static class MetaParser
{
    /// <summary>Parses a <c>.meta</c> file. Never fails: unreadable parts are treated as absent.</summary>
    public static MetaData Parse(string text)
    {
        var root = SimpleYaml.Parse(text);
        var guid = root.Get("guid")?.Trim().ToLowerInvariant();
        if (guid is not null && (guid.Length != 32 || !guid.All(Uri.IsHexDigit)))
        {
            guid = null;
        }

        var labels = root["labels"]?.Items?.Select(i => i.Scalar ?? string.Empty).Where(s => s.Length > 0).ToList() ?? [];
        var importer = root["PluginImporter"];
        return new MetaData(guid, labels, importer is null ? null : ParsePlugin(importer));
    }

    private static PluginSettings ParsePlugin(YamlNode importer)
    {
        var any = false;
        var excluded = new HashSet<string>(StringComparer.Ordinal);
        var enabled = new HashSet<string>(StringComparer.Ordinal);
        var entries = importer["platformData"]?.Items ?? [];
        foreach (var entry in entries)
        {
            var first = entry["first"]?.Map;
            var second = entry["second"];
            if (first is null || first.Count == 0 || second is null)
            {
                continue;
            }

            var (category, name) = (first[0].Key, first[0].Value.Scalar ?? string.Empty);
            var isEnabled = second.Get("enabled") == "1";
            if (category.Length == 0 && name == "Any")
            {
                // The ": Any" entry carries "Exclude <key>: 1" lines used when Any Platform is ticked.
                foreach (var setting in second["settings"]?.Map ?? [])
                {
                    if (setting.Key.StartsWith("Exclude ", StringComparison.Ordinal) && setting.Value.Scalar == "1")
                    {
                        excluded.Add(setting.Key["Exclude ".Length..]);
                    }
                }
            }
            else if (category == "Any" && name.Length == 0)
            {
                any = isEnabled;
            }
            else if (isEnabled)
            {
                enabled.Add(name.Length > 0 ? name : category);
            }
        }

        return new PluginSettings
        {
            HasPlatformData = entries.Count > 0,
            AnyPlatform = any,
            Excluded = excluded,
            Enabled = enabled,
            IsExplicitlyReferenced = importer.Get("isExplicitlyReferenced") == "1",
            ValidateReferences = importer.Get("validateReferences") != "0",
            DefineConstraints = importer["defineConstraints"]?.Items?.Select(i => i.Scalar ?? string.Empty).Where(s => s.Length > 0).ToList() ?? [],
        };
    }
}
