using UnityEngine;

namespace Game.Telemetry
{
    public static class TelemetryService
    {
        public static void Send(string eventName, string payload)
        {
            Debug.Log($"[Telemetry] {eventName}: {payload}");
        }
    }
}
