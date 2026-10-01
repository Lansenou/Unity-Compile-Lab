using Ucl.Core.Graph;
using Ucl.Core.Model;
using Ucl.Core.Results;

namespace Ucl.Core.Tests;

public class ResultsTests
{
    private static Diagnostic D(string id, string? file, int line = 0, int column = 0, string? assembly = null, string message = "m") =>
        new(id, Severity.Warning, DiagnosticOrigin.Compiler, assembly, file, line, column, message);

    [Fact]
    public void DiagnosticOrder_sorts_by_file_line_column_id_assembly_message()
    {
        var input = new[]
        {
            D("CS2", "Assets/B.cs", 1, 1),
            D("CS1", "Assets/A.cs", 10, 1),
            D("CS1", "Assets/A.cs", 2, 5),
            D("CS1", "Assets/A.cs", 2, 3),
            D("CS9", "Assets/A.cs", 2, 3),
            D("CS9", "Assets/A.cs", 2, 3, "Y"),
            D("CS9", "Assets/A.cs", 2, 3, "X", "b"),
            D("CS9", "Assets/A.cs", 2, 3, "X", "a"),
            D("UCL3", null),
        };
        var sorted = DiagnosticOrder.Sort(input);
        Assert.Equal([input[8], input[3], input[4], input[7], input[6], input[5], input[2], input[1], input[0]], sorted);
    }

    [Fact]
    public void Diagnostic_fields()
    {
        var d = new Diagnostic("CS0168", Severity.Error, DiagnosticOrigin.Analyzer, "Game", "Assets/A.cs", 3, 7, "msg", WarningAsError: true);
        Assert.Equal("CS0168", d.Id);
        Assert.Equal(Severity.Error, d.Severity);
        Assert.Equal(DiagnosticOrigin.Analyzer, d.Origin);
        Assert.Equal("Game", d.Assembly);
        Assert.Equal("Assets/A.cs", d.File);
        Assert.Equal(3, d.Line);
        Assert.Equal(7, d.Column);
        Assert.Equal("msg", d.Message);
        Assert.True(d.WarningAsError);
    }

    [Fact]
    public void Result_records_hold_their_values()
    {
        var diag = D("CS1", "Assets/A.cs");
        var assembly = new AssemblyResult
        {
            Name = "Game",
            Kind = AssemblyKind.Asmdef,
            DefinitionPath = "Assets/Game/Game.asmdef",
            Status = AssemblyStatus.Skipped,
            SkipReason = "dependency Core failed",
            InputsHash = "abc",
            Defines = ["A"],
            References = ["assembly:Core"],
            Analyzers = ["Assets/X.dll"],
            SourceCount = 3,
            Diagnostics = [diag],
            ElapsedMs = 12,
            Cached = true,
        };
        Assert.Equal("Game", assembly.Name);
        Assert.Equal(AssemblyKind.Asmdef, assembly.Kind);
        Assert.Equal("Assets/Game/Game.asmdef", assembly.DefinitionPath);
        Assert.Equal(AssemblyStatus.Skipped, assembly.Status);
        Assert.Equal("dependency Core failed", assembly.SkipReason);
        Assert.Equal("abc", assembly.InputsHash);
        Assert.Equal(["A"], assembly.Defines);
        Assert.Equal(["assembly:Core"], assembly.References);
        Assert.Equal(["Assets/X.dll"], assembly.Analyzers);
        Assert.Equal(3, assembly.SourceCount);
        Assert.Equal([diag], assembly.Diagnostics);
        Assert.Equal(12, assembly.ElapsedMs);
        Assert.True(assembly.Cached);

        var empty = new AssemblyResult { Name = "X" };
        Assert.Equal(AssemblyStatus.Compiled, empty.Status);
        Assert.Equal(string.Empty, empty.InputsHash);
        Assert.Empty(empty.Defines);

        var cell = new CellResult
        {
            Cell = Cells.Editor(),
            Assemblies = [assembly],
            Excluded = new SortedDictionary<string, string> { ["T"] = "r" },
            Diagnostics = [diag],
            Problems = [new Problem("UCL3004", "bad", "Assets/x.asmdef")],
            ExitCode = 1,
        };
        Assert.Equal(Cells.Editor(), cell.Cell);
        Assert.Same(assembly, Assert.Single(cell.Assemblies));
        Assert.Equal("r", cell.Excluded["T"]);
        Assert.Single(cell.Diagnostics);
        Assert.Equal("Assets/x.asmdef", Assert.Single(cell.Problems).File);
        Assert.Equal(1, cell.ExitCode);
        Assert.Empty(new CellResult { Cell = Cells.Player() }.Excluded);

        var run = new RunResult { ToolVersion = "0.1.0", Cells = [cell], Problems = [], Timings = true, ExitCode = 1 };
        Assert.Equal("0.1.0", run.ToolVersion);
        Assert.Single(run.Cells);
        Assert.Empty(run.Problems);
        Assert.True(run.Timings);
        Assert.Equal(1, run.ExitCode);
    }
}
