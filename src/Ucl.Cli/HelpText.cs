namespace Ucl.Cli;

/// <summary>The <c>ucl --help</c> text.</summary>
public static class HelpText
{
    /// <summary>Usage text.</summary>
    public const string Usage = """
        ucl - compile a Unity 6 project's C# without Unity (Roslyn + Unity's assembly rules)

        usage:
          ucl check [<project>] [options]      compile every assembly (default command)
          ucl test [<project>] [options]       run EditMode tests under .NET and classify every case
                                               (--filter <regex>, --format text|json|junit|nunit3,
                                               --emit-unity-filter <file>, --emit-unity-test-list <file>;
                                               docs/test.md)
          ucl graph [<project>] [options]      print the assembly graph (--format text|dot|json)
          ucl explain <file.cs> [--project P]  which assembly owns a file, with which defines and why
          ucl bee-diff [<project>]             compare ucl's compiler inputs with the Editor's own
                                               (Library/Bee/artifacts/*.dag/*.rsp; --format text|json|sarif)
          ucl export-csproj [<project>] --out <dir>   write IDE-style .csproj files (optional)
          ucl fetch [<project>]                fill the package download cache from the registries
          ucl doctor [<project>]               check the environment (editors, packages, cache)
          ucl version                          print the version

        matrix options (repeatable; every combination is one cell):
          --unity-version <v>     editor version (default: ProjectSettings/ProjectVersion.txt)
          --target editor|player  what the Editor compiles, or a player build (default: editor)
          --platform <p>          StandaloneWindows64 (default), StandaloneOSX, StandaloneLinux64,
                                  iOS, Android, WebGL

        options:
          --editor <path>         editor install to use (else UNITY_EDITOR_PATH, UCL_EDITOR_ROOTS, Unity Hub)
          --editor-os <os>        windows|macos|linux: UNITY_EDITOR_<OS> define (default: this machine)
          --backend mono|il2cpp   override the scripting backend
          --development           development player build (DEVELOPMENT_BUILD)
          --include-tests         compile test assemblies in player cells
          --analyzers on|off      run RoslynAnalyzer-labelled analyzers (default on)
          --warnaserror           treat every warning as an error
          --format text|json|sarif   output format (default text)
          --output <file>         write the report to a file instead of stdout
          --cache-dir <dir>       incremental cache (default <project>/Library/ucl)
          --no-cache              do not read or write the cache
          --changed <git-ref>     compile only assemblies whose inputs changed since <git-ref>, and dependents
          --summary               text: only each cell's root failures (failed assemblies, ranked by how many
                                  others they block, with their most frequent errors) and result line
          --timings               report per-assembly times (test: also per-phase wall times on stderr)
          --jobs <n>              parallel compiles (default: processor count)

        test host options (see docs/test.md):
          --host                 run compatible engine cases in a cached Windows Mono player
          --nographics           disable player graphics (host mode only; may change outcomes)
          --editor-cases <file>   exact full names with independently audited Editor ownership

        exit codes:
          0  clean (warnings allowed)
          1  compile, analyzer or ucl rule errors
          2  only warnings promoted to errors (-warnaserror, --warnaserror)
          3  configuration problem (missing editor, unresolved package, bad asmdef, unsupported version)
          4  internal error

        ucl never writes inside Assets/, Packages/ or ProjectSettings/, and only to Library/ucl in Library/.
        """;
}
