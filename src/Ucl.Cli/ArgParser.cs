using System.Globalization;
using Ucl.Core.Model;

namespace Ucl.Cli;

/// <summary>Parses argv. No prompts, no partial matches: an unknown option is an error (exit 3).</summary>
public static class ArgParser
{
    private static readonly string[] Commands = ["check", "test", "graph", "explain", "bee-diff", "export-csproj", "fetch", "doctor", "version", "help"];

    /// <summary>Parses arguments.</summary>
    public static Result<CliOptions> Parse(IReadOnlyList<string> args)
    {
        var o = new CliOptions();
        var i = 0;
        if (args.Count > 0 && Commands.Contains(args[0], StringComparer.Ordinal))
        {
            o = o with { Command = args[0] };
            i = 1;
        }
        else if (args.Count > 0 && args[0] is "--help" or "-h")
        {
            return Result<CliOptions>.Success(o with { Command = "help" });
        }
        else if (args.Count > 0 && args[0] == "--version")
        {
            return Result<CliOptions>.Success(o with { Command = "version" });
        }

        var positionals = new List<string>();
        var targets = new List<TargetKind>();
        var platforms = new List<BuildPlatform>();
        var versions = new List<UnityVersion>();
        for (; i < args.Count; i++)
        {
            var a = args[i];
            string Next()
            {
                if (i + 1 >= args.Count)
                {
                    throw new ArgumentException($"option {a} needs a value");
                }

                return args[++i];
            }

            try
            {
                switch (a)
                {
                    case "--target":
                        targets.Add(Next() switch
                        {
                            "editor" => TargetKind.Editor,
                            "player" => TargetKind.Player,
                            var t => throw new ArgumentException($"--target must be editor or player, not '{t}'"),
                        });
                        break;
                    case "--platform":
                        var p = PlatformInfo.Parse(Next());
                        platforms.Add(p.Ok ? p.Value : throw new ArgumentException(p.Error));
                        break;
                    case "--unity-version":
                        var v = UnityVersion.Parse(Next());
                        versions.Add(v.Ok ? v.Value! : throw new ArgumentException(v.Error));
                        break;
                    case "--editor": o = o with { EditorPath = Next() }; break;
                    case "--editor-os":
                        o = o with
                        {
                            EditorOs = Next() switch
                            {
                                "windows" => HostOs.Windows,
                                "macos" => HostOs.MacOS,
                                "linux" => HostOs.Linux,
                                var x => throw new ArgumentException($"--editor-os must be windows, macos or linux, not '{x}'"),
                            },
                        };
                        break;
                    case "--backend":
                        o = o with
                        {
                            Backend = Next() switch
                            {
                                "mono" => ScriptingBackend.Mono,
                                "il2cpp" => ScriptingBackend.IL2CPP,
                                var x => throw new ArgumentException($"--backend must be mono or il2cpp, not '{x}'"),
                            },
                        };
                        break;
                    case "--development": o = o with { Development = true }; break;
                    case "--include-tests": o = o with { IncludeTests = true }; break;
                    case "--analyzers":
                        o = o with
                        {
                            Analyzers = Next() switch
                            {
                                "on" => true,
                                "off" => false,
                                var x => throw new ArgumentException($"--analyzers must be on or off, not '{x}'"),
                            },
                        };
                        break;
                    case "--warnaserror": o = o with { WarnAsError = true }; break;
                    case "--format":
                        var f = Next();
                        o = o with { Format = f is "text" or "json" or "sarif" or "dot" or "junit" or "nunit3" ? f : throw new ArgumentException($"--format must be text, json or sarif (dot for graph; junit or nunit3 for test), not '{f}'") };
                        break;
                    case "--output" or "-o": o = o with { Output = Next() }; break;
                    case "--cache-dir": o = o with { CacheDir = Next() }; break;
                    case "--no-cache": o = o with { NoCache = true }; break;
                    case "--changed": o = o with { Changed = Next() }; break;
                    case "--timings": o = o with { Timings = true }; break;
                    case "--summary": o = o with { Summary = true }; break;
                    case "--host": o = o with { Host = true }; break;
                    case "--nographics": o = o with { NoGraphics = true }; break;
                    case "--editor-cases": o = o with { EditorCases = Next() }; break;
                    case "--filter": o = o with { Filter = Next() }; break;
                    case "--emit-unity-filter": o = o with { EmitUnityFilter = Next() }; break;
                    case "--jobs" or "-j":
                        var n = Next();
                        o = o with { Jobs = int.TryParse(n, NumberStyles.None, CultureInfo.InvariantCulture, out var j) && j > 0 ? j : throw new ArgumentException($"--jobs needs a positive number, not '{n}'") };
                        break;
                    case "--project": o = o with { Project = Next() }; break;
                    case "--out": o = o with { OutDir = Next() }; break;
                    case "--help" or "-h": o = o with { Command = "help" }; break;
                    default:
                        if (a.StartsWith('-') && a.Length > 1)
                        {
                            throw new ArgumentException($"unknown option '{a}' (see ucl --help)");
                        }

                        positionals.Add(a);
                        break;
                }
            }
            catch (ArgumentException e)
            {
                return Result<CliOptions>.Failure(e.Message);
            }
        }

        if (o.Host && o.Command != "test" || (o.NoGraphics || o.EditorCases is not null) && !o.Host)
            return Result<CliOptions>.Failure("--host is for test; --nographics and --editor-cases require --host.");

        return Result<CliOptions>.Success(o with
        {
            Positionals = positionals,
            Targets = targets.Distinct().ToList(),
            Platforms = platforms.Distinct().ToList(),
            UnityVersions = versions.Distinct().ToList(),
        });
    }
}
