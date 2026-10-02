// Stand-in for the editor's own source generators, written for the ucl fixtures. Not Unity code. Apache-2.0.

using Microsoft.CodeAnalysis;

namespace Ucl.Fixture.EditorGenerators
{
    /// <summary>Adds the internal <c>UclEditorGenerated.EditorGeneratorMarker</c> to every compilation it runs on.</summary>
    [Generator(LanguageNames.CSharp)]
    public sealed class EditorMarkerGenerator : IIncrementalGenerator
    {
        private const string Source =
            "namespace UclEditorGenerated { internal static class EditorGeneratorMarker { public const int Value = 6000; } }\n";

        /// <inheritdoc />
        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            context.RegisterPostInitializationOutput(ctx => ctx.AddSource("EditorGeneratorMarker.g.cs", Source));
        }
    }
}
