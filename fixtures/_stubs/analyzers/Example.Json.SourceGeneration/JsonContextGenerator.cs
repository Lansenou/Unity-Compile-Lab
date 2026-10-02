// Fixture source generator shaped like a NuGet package's analyzers/dotnet/roslyn4.0/cs/ generator. Original code, Apache-2.0.

using Microsoft.CodeAnalysis;

namespace Example.Json.SourceGeneration
{
    /// <summary>Adds the internal <c>Example.Json.Generated.JsonContextMarker</c> to every compilation it runs on.</summary>
    [Generator(LanguageNames.CSharp)]
    public sealed class JsonContextGenerator : IIncrementalGenerator
    {
        private const string Source =
            "namespace Example.Json.Generated { internal static class JsonContextMarker { public const int Value = 7; } }\n";

        /// <inheritdoc />
        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            context.RegisterPostInitializationOutput(ctx => ctx.AddSource("JsonContextMarker.g.cs", Source));
        }
    }
}
