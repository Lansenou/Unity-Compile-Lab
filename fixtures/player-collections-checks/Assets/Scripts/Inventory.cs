// Fixture source for the ucl conformance corpus. Original code, Apache-2.0.
using UnityEngine;

public class Inventory : MonoBehaviour
{
    private Unity.Collections.NativeList m_Items;

    private void Start() => Debug.Log(m_Items.AsReadOnly().Length);
}
