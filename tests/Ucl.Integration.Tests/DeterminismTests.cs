using Xunit;

namespace Ucl.Integration.Tests;

/// <summary>R9: two runs produce byte-identical output in every format.</summary>
public sealed class DeterminismTests
{
    /// <summary>JSON, SARIF and text output are byte-identical across runs for every fixture.</summary>
    [Theory]
    [InlineData("json")]
    [InlineData("sarif")]
    [InlineData("text")]
    public void Output_is_byte_identical(string format)
    {
        foreach (var entry in FixtureManifest.Load().Fixtures)
        {
            using var temp = new TempDir();
            var project = FixtureRunner.Prepare(entry, temp.Path);
            var home = Path.Combine(temp.Path, "home");
            Directory.CreateDirectory(home);
            var cell = entry.Cells[0];
            var args = FixtureRunner.Args(project, cell, Path.Combine(temp.Path, "cache"));
            args[args.IndexOf("json")] = format;
            var first = Cli.Run(new TestEnvironment(home), [.. args]);
            var second = Cli.Run(new TestEnvironment(home), [.. args]);
            Assert.Equal(first.Stdout, second.Stdout);
            Assert.Equal(first.Exit, second.Exit);
            Assert.DoesNotContain(project, first.Stdout, StringComparison.Ordinal);
        }
    }
}
