// Fixture source for the ucl conformance corpus. Original code, Apache-2.0.

using System.Collections.Generic;
using Unity.CompilationPipeline.Common.Diagnostics;
using Unity.CompilationPipeline.Common.ILPostProcessing;

namespace Example.Weaver.CodeGen
{
    internal sealed class WeaverPostProcessor : ILPostProcessor
    {
        public override ILPostProcessor GetInstance() => this;

        public override bool WillProcess(ICompiledAssembly compiledAssembly) => compiledAssembly.Name == "Assembly-CSharp";

        public override ILPostProcessResult Process(ICompiledAssembly compiledAssembly)
        {
            var messages = new List<DiagnosticMessage> { new DiagnosticMessage { DiagnosticType = DiagnosticType.Warning, MessageData = "woven" } };
            return new ILPostProcessResult(compiledAssembly.InMemoryAssembly, messages);
        }
    }
}
