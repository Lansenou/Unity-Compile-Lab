// Original stand-in for part of the Unity API, written for the ucl fixtures. Not Unity code. Apache-2.0.

using System.Collections.Generic;
using Unity.CompilationPipeline.Common.Diagnostics;

namespace Unity.CompilationPipeline.Common.ILPostProcessing
{
    /// <summary>An assembly handed to an IL post-processor.</summary>
    public interface ICompiledAssembly
    {
        InMemoryAssembly InMemoryAssembly { get; }

        string Name { get; }

        string[] References { get; }

        string[] Defines { get; }
    }

    /// <summary>The bytes of a compiled assembly.</summary>
    public class InMemoryAssembly
    {
        public InMemoryAssembly(byte[] peData, byte[] pdbData) => throw null;

        public byte[] PeData => throw null;

        public byte[] PdbData => throw null;
    }

    /// <summary>The output of an IL post-processor.</summary>
    public class ILPostProcessResult
    {
        public ILPostProcessResult(InMemoryAssembly inMemoryAssembly) => throw null;

        public ILPostProcessResult(InMemoryAssembly inMemoryAssembly, List<DiagnosticMessage> diagnostics) => throw null;

        public InMemoryAssembly InMemoryAssembly => throw null;

        public List<DiagnosticMessage> Diagnostics => throw null;
    }

    /// <summary>Rewrites compiled assemblies after compilation.</summary>
    public abstract class ILPostProcessor
    {
        public abstract ILPostProcessor GetInstance();

        public abstract bool WillProcess(ICompiledAssembly compiledAssembly);

        public abstract ILPostProcessResult Process(ICompiledAssembly compiledAssembly);
    }
}

namespace Unity.CompilationPipeline.Common.Diagnostics
{
    /// <summary>Severity of a <see cref="DiagnosticMessage"/>.</summary>
    public enum DiagnosticType
    {
        Error = 1,
        Warning = 2,
    }

    /// <summary>A message reported by an IL post-processor.</summary>
    public class DiagnosticMessage
    {
        public DiagnosticType DiagnosticType { get; set; }

        public string MessageData { get; set; }

        public string File { get; set; }

        public int Line { get; set; }

        public int Column { get; set; }
    }
}
