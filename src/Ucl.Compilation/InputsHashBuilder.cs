using System.Security.Cryptography;
using System.Text;
using Ucl.Core.Graph;

namespace Ucl.Compilation;

/// <summary>Accumulates every input of one assembly compile into a SHA-256; the incremental cache key.</summary>
internal sealed class InputsHashBuilder
{
    private readonly List<string> _lines = [];

    public InputsHashBuilder(string toolVersion, AssemblyPlan plan)
    {
        _lines.Add($"tool {toolVersion}");
        _lines.Add($"name {plan.Name}");
        _lines.Add($"defines {string.Join(';', plan.Defines.Symbols)}");
        _lines.Add($"options lang={plan.LangVersion} nullable={plan.Nullable} unsafe={plan.AllowUnsafe} wae={plan.WarnAsErrorAll}");
        _lines.Add($"nowarn {string.Join(';', plan.NoWarn)} waeids {string.Join(';', plan.WarnAsErrorIds)} wnaeids {string.Join(';', plan.WarnNotAsErrorIds)}");
        _lines.Add($"ruleset {plan.RuleSet} additional {string.Join(';', plan.AdditionalFiles)}");
        _lines.Add($"suppress-warnings {plan.SuppressWarnings}");
    }

    public void Add(string kind, string name, string hash) => _lines.Add($"{kind} {name} {hash}");

    public string Finish(bool globalWarnAsError)
    {
        _lines.Add($"global-wae {globalWarnAsError}");
        _lines.Sort(StringComparer.Ordinal);
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('\n', _lines))));
    }
}
