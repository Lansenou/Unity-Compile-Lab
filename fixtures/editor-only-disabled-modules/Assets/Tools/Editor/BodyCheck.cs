// Fixture source for the ucl conformance corpus. Original code, Apache-2.0.
using UnityEngine;

public static class BodyCheck
{
    public static bool Has(GameObject go) => go.GetComponent<Rigidbody>() != null;
}
