// Fixture source for the ucl conformance corpus. Original code, Apache-2.0.

using Example.Collections;
using UnityEngine;

public class Inventory : MonoBehaviour
{
    public int Count(NativeList list) => list.AsReadOnly().Length;
#if UNITY_EDITOR_ONLY_COMPILATION
    // Assembly-CSharp is not Editor-only: this line must never compile.
    private Missing m_Missing;
#endif
}
