using System.Text.Json;
using Xunit;

namespace Ucl.Integration.Tests;

/// <summary>Runs <c>ucl check</c> over every cell of fixtures/manifest.json and compares the JSON report exactly.</summary>
public sealed class FixtureTests
{
    /// <summary>Every (fixture, cell) pair that runs without a real editor.</summary>
    public static TheoryData<string, int> Cells()
    {
        var data = new TheoryData<string, int>();
        var real = Environment.GetEnvironmentVariable("UCL_REAL_EDITOR") == "1";
        foreach (var f in FixtureManifest.Load().Fixtures)
        {
            for (var i = 0; i < f.Cells.Count; i++)
            {
                if (!f.Cells[i].RealEditor || real)
                {
                    data.Add(f.Name, i);
                }
            }
        }

        return data;
    }

    /// <summary>One manifest cell.</summary>
    [Theory]
    [MemberData(nameof(Cells))]
    public void Cell_matches_manifest(string fixture, int index)
    {
        var entry = FixtureManifest.Load().Fixtures.Single(f => f.Name == fixture);
        var cell = entry.Cells[index];
        using var temp = new TempDir();
        var project = FixtureRunner.Prepare(entry, temp.Path);
        var report = FixtureRunner.Check(project, cell, temp.Path);

        var json = JsonDocument.Parse(report.Stdout).RootElement;
        var cellJson = json.GetProperty("cells")[0];
        var context = $"{fixture} cell {index} ({cell.Target} {cell.Platform} {cell.UnityVersion})\n{report.Stdout}";

        Assert.True(cell.ExitCode == report.Exit, $"exit {report.Exit}, expected {cell.ExitCode}: {context}");
        var assemblies = cellJson.GetProperty("assemblies").EnumerateArray().ToDictionary(a => a.GetProperty("name").GetString()!, a => a);
        if (cell.Assemblies is { } expectedAssemblies)
        {
            Assert.True(
                expectedAssemblies.Select(a => a.Name).Order(StringComparer.Ordinal).SequenceEqual(assemblies.Keys.Order(StringComparer.Ordinal)),
                $"assemblies [{string.Join(", ", assemblies.Keys.Order(StringComparer.Ordinal))}]: {context}");
            foreach (var expected in expectedAssemblies)
            {
                var defines = assemblies[expected.Name].GetProperty("defines").EnumerateArray().Select(d => d.GetString()!).ToHashSet(StringComparer.Ordinal);
                if (expected.Defines is { } exact)
                {
                    Assert.Equal(exact.Order(StringComparer.Ordinal), defines.Order(StringComparer.Ordinal));
                }

                foreach (var d in expected.DefinesInclude ?? [])
                {
                    Assert.True(defines.Contains(d), $"{expected.Name} lacks {d}: {context}");
                }

                foreach (var d in expected.DefinesExclude ?? [])
                {
                    Assert.False(defines.Contains(d), $"{expected.Name} has {d}: {context}");
                }
            }
        }

        if (cell.Excluded is { } excluded)
        {
            var actual = cellJson.GetProperty("excluded").EnumerateArray().Select(e => e.GetProperty("name").GetString()!).Order(StringComparer.Ordinal);
            Assert.True(excluded.Order(StringComparer.Ordinal).SequenceEqual(actual), $"excluded [{string.Join(", ", actual)}]: {context}");
        }

        var diagnostics = cellJson.GetProperty("diagnostics").EnumerateArray()
            .Select(d => new ExpectedDiagnostic(
                d.GetProperty("id").GetString()!,
                d.GetProperty("severity").GetString()!,
                d.TryGetProperty("file", out var f) ? f.GetString() : null,
                d.GetProperty("line").GetInt32(),
                d.GetProperty("column").GetInt32()).ToString())
            .Order(StringComparer.Ordinal)
            .ToList();
        var expectedDiagnostics = (cell.Diagnostics ?? []).Select(d => d.ToString()).Order(StringComparer.Ordinal).ToList();
        Assert.True(expectedDiagnostics.SequenceEqual(diagnostics), $"diagnostics\n  expected: {string.Join("\n            ", expectedDiagnostics)}\n  actual:   {string.Join("\n            ", diagnostics)}\n{context}");
    }

    /// <summary>The manifest lists every fixture folder and nothing else.</summary>
    [Fact]
    public void Manifest_covers_every_fixture_folder()
    {
        var folders = Directory.GetDirectories(Repo.Fixtures).Select(Path.GetFileName).Where(n => !n!.StartsWith('_')).Order(StringComparer.Ordinal);
        var listed = FixtureManifest.Load().Fixtures.Select(f => f.Name).Order(StringComparer.Ordinal);
        Assert.Equal(listed, folders);
    }

    /// <summary>Every cell is marked for the oracle.</summary>
    [Fact]
    public void Every_cell_has_an_oracle_status()
    {
        foreach (var cell in FixtureManifest.Load().Fixtures.SelectMany(f => f.Cells))
        {
            Assert.Contains(cell.Oracle, new[] { "pending", "agrees", "disagrees" });
        }
    }
}
