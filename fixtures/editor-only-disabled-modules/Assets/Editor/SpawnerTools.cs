// Fixture source for the ucl conformance corpus. Original code, Apache-2.0.
using UnityEngine;

public static class SpawnerTools
{
    public static void AddBody(GameObject go)
    {
        var body = go.AddComponent<Rigidbody>();
        body.AddForce(Vector3.up, ForceMode.Impulse);
    }
}
