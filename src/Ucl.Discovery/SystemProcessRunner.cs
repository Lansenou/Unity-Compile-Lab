using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Ucl.Discovery;

/// <summary><see cref="IProcessRunner"/> over <see cref="Process"/>.</summary>
public sealed class SystemProcessRunner : IProcessRunner
{
    /// <inheritdoc/>
    public async Task<ProcessResult> RunAsync(string fileName, IReadOnlyList<string> arguments, string workingDirectory, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        var info = new ProcessStartInfo(fileName)
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (var a in arguments)
        {
            info.ArgumentList.Add(a);
        }

        using var process = new Process { StartInfo = info };
        try
        {
            process.Start();
        }
        catch (ExternalException e)
        {
            // A missing program (Win32Exception) is an expected answer (doctor asks "is git installed?"), not a bug.
            // Its base type is caught because Win32Exception lives in an assembly the architecture test does not allow.
            return new ProcessResult(-1, string.Empty, e.Message, Started: false);
        }

        // Nothing is ever piped in; closing stdin stops a child that would otherwise wait for input.
        process.StandardInput.Close();

        // Both streams are drained concurrently: a child that fills one pipe while we wait on the other would deadlock.
        var stdout = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderr = process.StandardError.ReadToEndAsync(cancellationToken);
        try
        {
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            throw;
        }

        return new ProcessResult(process.ExitCode, await stdout.ConfigureAwait(false), await stderr.ConfigureAwait(false));
    }
}
