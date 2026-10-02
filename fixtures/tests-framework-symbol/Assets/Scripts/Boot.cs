// Fixture source for the ucl conformance corpus. Original code, Apache-2.0.
using UnityEngine;

public class Boot : MonoBehaviour
{
    private void Start() => Debug.Log(new Example.Input.InputDevice().Name);
}
