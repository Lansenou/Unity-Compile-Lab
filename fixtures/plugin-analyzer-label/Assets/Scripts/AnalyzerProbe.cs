public static class AnalyzerProbe
{
    // The analyzer DLL is not a reference: its types are not visible to scripts.
    public static string Id => Ucl.Fixture.Analyzer.BadNameAnalyzer.DiagnosticId;
}
