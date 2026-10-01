using System;
using UnityEngine;

public static class InventoryService
{
    [Obsolete("Use AddItem(string, int) instead.")]
    public static void AddItem(string id)
    {
        AddItem(id, 1);
    }

    public static void AddItem(string id, int count)
    {
        Debug.Log($"Added {count} x {id}");
    }
}
