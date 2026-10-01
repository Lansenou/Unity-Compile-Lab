namespace Ucl.Discovery;

/// <summary>Port to child processes (<c>git</c> for <c>--changed</c> and <c>ucl doctor</c>).</summary>
public interface IProcessRunner
{
    /// <summary>Runs <paramref name="fileName"/> to completion and captures its output.</summary>
    /// <param name="fileName">Program name (looked up on <c>PATH</c>) or path.</param>
    /// <param name="arguments">Arguments, passed without shell interpretation.</param>
    /// <param name="workingDirectory">Working directory of the child process.</param>
    /// <param name="cancellationToken">Kills the process when cancelled.</param>
    /// <returns>Exit code and output; <see cref="ProcessResult.Started"/> is false when the program could not be started.</returns>
    Task<ProcessResult> RunAsync(string fileName, IReadOnlyList<string> arguments, string workingDirectory, CancellationToken cancellationToken = default);
}
