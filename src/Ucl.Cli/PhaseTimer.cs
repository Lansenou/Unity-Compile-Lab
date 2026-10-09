using System.Diagnostics;
using System.Globalization;

namespace Ucl.Cli;

/// <summary>
/// <c>--timings</c> for <c>ucl test</c>: one stderr line per sequential phase, <c>timing: &lt;phase&gt; &lt;seconds&gt; s</c>.
/// Each mark covers the wall time since the previous mark, so the phases add up to the whole run.
/// </summary>
internal sealed class PhaseTimer(TextWriter? sink)
{
    private readonly Stopwatch watch = Stopwatch.StartNew();
    private TimeSpan last;

    /// <summary>Records the wall time since the previous mark (or the start) as <paramref name="phase"/>.</summary>
    public void Mark(string phase)
    {
        var now = watch.Elapsed;
        Write(phase, now - last);
        last = now;
    }

    /// <summary>Records a measured part of the current phase without closing it.</summary>
    public void Detail(string phase, TimeSpan elapsed) => Write(phase, elapsed);

    private void Write(string phase, TimeSpan elapsed) =>
        sink?.WriteLine("timing: " + phase + " " + elapsed.TotalSeconds.ToString("0.000", CultureInfo.InvariantCulture) + " s");
}
