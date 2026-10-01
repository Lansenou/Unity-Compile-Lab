using System.Collections.Generic;
using UnityEngine;

public static class AnalyticsClient
{
    private static readonly List<string> Pending = new List<string>();

    public static void Track(string eventName)
    {
        Pending.Add(eventName);
        Debug.Log($"[Analytics] {eventName} ({Pending.Count} queued)");
    }
}
