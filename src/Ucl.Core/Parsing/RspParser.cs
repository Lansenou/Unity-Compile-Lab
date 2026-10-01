using System.Text;

namespace Ucl.Core.Parsing;

/// <summary>Parses <c>csc.rsp</c> files and <c>additionalCompilerArguments</c> with csc's quoting rules.</summary>
public static class RspParser
{
    /// <summary>Parses a response file's text.</summary>
    public static RspOptions Parse(string text) => Parse(Tokenize(text));

    /// <summary>Parses already split arguments.</summary>
    public static RspOptions Parse(IEnumerable<string> args)
    {
        var defines = new List<string>();
        var nowarn = new List<string>();
        var waeIds = new List<string>();
        var waeNotIds = new List<string>();
        var additional = new List<string>();
        var configs = new List<string>();
        var refs = new List<string>();
        var unsupported = new List<string>();
        bool? wae = null, @unsafe = null;
        string? lang = null, nullable = null, ruleset = null;

        foreach (var rawArg in args)
        {
            var arg = rawArg.Length > 1 && rawArg[0] == '"' && rawArg[^1] == '"' ? rawArg[1..^1] : rawArg;
            if (arg.Length < 2 || (arg[0] != '-' && arg[0] != '/'))
            {
                unsupported.Add(arg);
                continue;
            }

            var body = arg[1..];
            var colon = body.IndexOf(':', StringComparison.Ordinal);
            var name = (colon < 0 ? body : body[..colon]).ToLowerInvariant();
            var value = colon < 0 ? null : body[(colon + 1)..].Trim('"');
            switch (name)
            {
                case "define" or "d" when value is not null:
                    defines.AddRange(ProjectSettingsParser.SplitDefines(value));
                    break;
                case "nowarn" when value is not null:
                    nowarn.AddRange(Ids(value));
                    break;
                case "warnaserror" or "warnaserror+":
                    if (value is null) wae = true;
                    else waeIds.AddRange(Ids(value));
                    break;
                case "warnaserror-":
                    if (value is null) wae = false;
                    else waeNotIds.AddRange(Ids(value));
                    break;
                case "langversion" when value is not null:
                    lang = value;
                    break;
                case "nullable":
                    nullable = value ?? "enable";
                    break;
                case "nullable+":
                    nullable = "enable";
                    break;
                case "nullable-":
                    nullable = "disable";
                    break;
                case "unsafe" or "unsafe+":
                    @unsafe = true;
                    break;
                case "unsafe-":
                    @unsafe = false;
                    break;
                case "additionalfile" when value is not null:
                    additional.AddRange(value.Split([';', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
                    break;
                case "analyzerconfig" when value is not null:
                    configs.AddRange(value.Split([';', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
                    break;
                case "ruleset" when value is not null:
                    ruleset = value;
                    break;
                case "r" or "reference" when value is not null:
                    refs.AddRange(value.Split([';', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
                    break;
                default:
                    unsupported.Add(arg);
                    break;
            }
        }

        return new RspOptions
        {
            Defines = defines,
            NoWarn = nowarn,
            WarnAsErrorAll = wae,
            WarnAsErrorIds = waeIds,
            WarnNotAsErrorIds = waeNotIds,
            LangVersion = lang,
            Nullable = nullable,
            Unsafe = @unsafe,
            AdditionalFiles = additional,
            AnalyzerConfigs = configs,
            RuleSet = ruleset,
            References = refs,
            Unsupported = unsupported,
        };
    }

    /// <summary>Normalises <c>-nowarn</c> ids: <c>618</c> and <c>0618</c> become <c>CS0618</c>; other ids are kept.</summary>
    public static IEnumerable<string> Ids(string value) =>
        value.Split([';', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(id => id.All(char.IsAsciiDigit) ? "CS" + id.TrimStart('0').PadLeft(4, '0') : id);

    /// <summary>Splits response-file text into arguments: whitespace separated, double quotes group, <c>#</c> starts a comment line.</summary>
    public static List<string> Tokenize(string text)
    {
        var result = new List<string>();
        foreach (var rawLine in text.Split('\n'))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line[0] == '#')
            {
                continue;
            }

            var current = new StringBuilder();
            var quoted = false;
            foreach (var c in line)
            {
                if (c == '"')
                {
                    quoted = !quoted;
                    current.Append(c);
                }
                else if (char.IsWhiteSpace(c) && !quoted)
                {
                    if (current.Length > 0) result.Add(current.ToString());
                    current.Clear();
                }
                else
                {
                    current.Append(c);
                }
            }

            if (current.Length > 0) result.Add(current.ToString());
        }

        return result;
    }
}
