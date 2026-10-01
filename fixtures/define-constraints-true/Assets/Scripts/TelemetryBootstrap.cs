using UnityEngine;

public class TelemetryBootstrap : MonoBehaviour
{
    private void Start()
    {
#if ENABLE_TELEMETRY
        Game.Telemetry.TelemetryService.Send("boot", Application.version);
#else
        Debug.Log("Telemetry is disabled for this build target");
#endif
    }
}
