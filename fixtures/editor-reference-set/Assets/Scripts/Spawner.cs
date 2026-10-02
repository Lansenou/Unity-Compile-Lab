// Fixture source for the ucl conformance corpus. Original code, Apache-2.0.

using UnityEngine;

public class Spawner : MonoBehaviour
{
    // Vendor.Legacy was built against the single UnityEngine assembly: only the UnityEngine.dll facade resolves it.
    public float Height() => Vendor.Legacy.Anchors.Origin.y;
}
