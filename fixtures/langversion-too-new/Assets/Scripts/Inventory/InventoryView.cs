using UnityEngine;

public class InventoryView : MonoBehaviour
{
    [SerializeField] private int columns = 4;

    public int Rows(int slotCount)
    {
        return Mathf.Max(1, (slotCount + columns - 1) / columns);
    }
}
