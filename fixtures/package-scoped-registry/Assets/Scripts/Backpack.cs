using Example.Inventory;
using UnityEngine;

public class Backpack : MonoBehaviour
{
    private readonly InventoryGrid grid = new InventoryGrid(6, 4);

    private void Start()
    {
        Debug.Log($"Backpack holds {grid.Capacity} items");
    }
}
