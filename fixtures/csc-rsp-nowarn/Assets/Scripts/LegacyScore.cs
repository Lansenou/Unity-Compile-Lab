using System;

public static class LegacyScore
{
    [Obsolete("Use ScoreService.Submit instead.")]
    public static void Report(int points)
    {
        ScoreService.Submit(points);
    }
}
