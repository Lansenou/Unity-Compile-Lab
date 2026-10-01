// Fixture source generator for the ucl conformance corpus. Original code, Apache-2.0.

using Microsoft.CodeAnalysis;

namespace Ucl.Fixture.Generator
{
    /// <summary>Adds <c>Ucl.Generated.GeneratedMarker</c> to every compilation it runs on.</summary>
    [Generator(LanguageNames.CSharp)]
    public sealed class MarkerGenerator : IIncrementalGenerator
    {
        private const string Source =
            "namespace Ucl.Generated { public static class GeneratedMarker { public const int Value = 42; } }\n";

        /// <inheritdoc />
        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            context.RegisterPostInitializationOutput(ctx => ctx.AddSource("GeneratedMarker.g.cs", Source));
        }
    }
}
