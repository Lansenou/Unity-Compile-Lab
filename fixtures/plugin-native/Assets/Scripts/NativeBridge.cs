using System.Runtime.InteropServices;
using UnityEngine;

// Calls into an unmanaged plugin. The DLL is loaded at run time; the compiler never references it.
public class NativeBridge : MonoBehaviour
{
    [DllImport("Ucl.Fixture.Native")]
    private static extern int ucl_answer();

    private void Start()
    {
        Debug.Log("native answer: " + ucl_answer());
    }
}
