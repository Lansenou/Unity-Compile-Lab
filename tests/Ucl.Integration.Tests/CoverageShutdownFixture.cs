using System.Runtime.CompilerServices;

namespace Ucl.Integration.Tests;

/// <summary>Public Coverlet/VSTest shutdown regression: delay exit before lazily loaded product modules flush their hits.</summary>
internal static class CoverageShutdownFixture
{
    [ModuleInitializer]
    internal static void Install()
    {
        if (Environment.GetEnvironmentVariable("UCL_COVERAGE_SLOW_EXIT") == "1")
        {
            AppDomain.CurrentDomain.ProcessExit += (_, _) => Thread.Sleep(2000);
        }
    }
}
