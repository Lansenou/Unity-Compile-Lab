using Ucl.Core.Graph;

namespace Ucl.Core.Tests;

/// <summary>docs/architecture.md: Ucl.Core depends on the BCL only.</summary>
public class ArchitectureTests
{
    [Fact]
    public void Core_references_only_the_base_class_library()
    {
        var references = typeof(AssemblyGraphBuilder).Assembly.GetReferencedAssemblies().Select(a => a.Name!).ToList();
        Assert.NotEmpty(references);
        Assert.All(references, name => Assert.True(
            name is "netstandard" or "mscorlib" || name == "System" || name.StartsWith("System.", StringComparison.Ordinal),
            $"Ucl.Core references '{name}', which is not part of the BCL"));
    }
}
