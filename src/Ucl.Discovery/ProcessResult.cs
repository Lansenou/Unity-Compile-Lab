namespace Ucl.Discovery;

/// <summary>The outcome of a child process.</summary>
/// <param name="ExitCode">Exit code, or -1 when the process did not start.</param>
/// <param name="Stdout">Captured standard output.</param>
/// <param name="Stderr">Captured standard error, or why the process did not start.</param>
/// <param name="Started">False when the program was not found or could not be executed.</param>
public sealed record ProcessResult(int ExitCode, string Stdout, string Stderr, bool Started = true);
