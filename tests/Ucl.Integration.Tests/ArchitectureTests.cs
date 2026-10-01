using System.Reflection;
using Xunit;

namespace Ucl.Integration.Tests;

/// <summary>Dependencies point inward (docs/architecture.md): checked by reflection over assembly references.</summary>
public sealed class ArchitectureTests
{
    private static readonly Dictionary<string, string[]> Allowed = new()
    {
        ["Ucl.Core"] = [],
        ["Ucl.Discovery"] = ["Ucl.Core"],
        ["Ucl.Compilation"] = ["Ucl.Core", "Ucl.Discovery", "Microsoft.CodeAnalysis", "Microsoft.CodeAnalysis.CSharp"],
        ["Ucl.Reporting"] = ["Ucl.Core"],
        ["ucl"] = ["Ucl.Core", "Ucl.Discovery", "Ucl.Compilation", "Ucl.Reporting"],
    };

    /// <summary>Each module references only the modules it may.</summary>
    [Theory]
    [InlineData("Ucl.Core")]
    [InlineData("Ucl.Discovery")]
    [InlineData("Ucl.Compilation")]
    [InlineData("Ucl.Reporting")]
    [InlineData("ucl")]
    public void References_point_inward(string name)
    {
        var assembly = Assembly.Load(name);
        var nonFramework = assembly.GetReferencedAssemblies()
            .Select(a => a.Name!)
            .Where(n => !n.StartsWith("System", StringComparison.Ordinal) && n != "netstandard" && n != "mscorlib")
            .ToList();
        Assert.All(nonFramework, n => Assert.Contains(n, Allowed[name]));
    }

    /// <summary>Each source file declares at most one public top-level type.</summary>
    [Fact]
    public void One_public_type_per_file()
    {
        foreach (var file in Directory.EnumerateFiles(Path.Combine(Repo.Root, "src"), "*.cs", SearchOption.AllDirectories))
        {
            if (file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            {
                continue;
            }

            var count = File.ReadLines(file).Count(l => System.Text.RegularExpressions.Regex.IsMatch(l, @"^public (sealed |static |abstract |readonly |partial )*(class|record|struct|interface|enum) "));
            Assert.True(count <= 1, $"{file} declares {count} public types");
        }
    }

    /// <summary>Files over 400 lines must be listed in docs/architecture.md with a reason.</summary>
    [Fact]
    public void Long_files_are_documented()
    {
        var doc = File.ReadAllText(Path.Combine(Repo.Root, "docs", "architecture.md"));
        foreach (var file in Directory.EnumerateFiles(Path.Combine(Repo.Root, "src"), "*.cs", SearchOption.AllDirectories))
        {
            var rel = Path.GetRelativePath(Repo.Root, file).Replace('\\', '/');
            if (!rel.Contains("/obj/", StringComparison.Ordinal) && File.ReadLines(file).Count() > 400)
            {
                Assert.Contains(rel, doc, StringComparison.Ordinal);
            }
        }
    }
}
