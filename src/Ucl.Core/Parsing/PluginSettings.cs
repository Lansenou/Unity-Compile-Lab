namespace Ucl.Core.Parsing;

/// <summary>Plugin import settings from a DLL's <c>.meta</c> (<c>PluginImporter</c>).</summary>
public sealed record PluginSettings
{
    /// <summary>The default for a DLL whose importer has no platform data: compatible everywhere, auto referenced.</summary>
    public static PluginSettings Default { get; } = new();

    /// <summary>True when the meta has any <c>platformData</c>; when false the plugin is compatible everywhere.</summary>
    public bool HasPlatformData { get; init; }

    /// <summary>"Any Platform" is ticked.</summary>
    public bool AnyPlatform { get; init; }

    /// <summary>Plugin keys excluded under Any Platform.</summary>
    public IReadOnlySet<string> Excluded { get; init; } = new HashSet<string>(StringComparer.Ordinal);

    /// <summary>Plugin keys explicitly enabled.</summary>
    public IReadOnlySet<string> Enabled { get; init; } = new HashSet<string>(StringComparer.Ordinal);

    /// <summary>"Auto Reference" is off (<c>isExplicitlyReferenced: 1</c>).</summary>
    public bool IsExplicitlyReferenced { get; init; }

    /// <summary>"Validate References".</summary>
    public bool ValidateReferences { get; init; } = true;

    /// <summary>Plugin define constraints.</summary>
    public IReadOnlyList<string> DefineConstraints { get; init; } = [];

    /// <summary>True when the plugin is compatible with the given <c>.meta</c> platform key.</summary>
    public bool IsCompatibleWith(string pluginKey)
    {
        if (!HasPlatformData)
        {
            return true;
        }

        return AnyPlatform ? !Excluded.Contains(pluginKey) : Enabled.Contains(pluginKey);
    }
}
