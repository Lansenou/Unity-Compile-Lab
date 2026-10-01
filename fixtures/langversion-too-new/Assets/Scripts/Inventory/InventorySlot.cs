namespace Game.Inventory;

public sealed class InventorySlot
{
    public InventorySlot(string itemId, int count)
    {
        ItemId = itemId;
        Count = count;
    }

    public string ItemId { get; }

    public int Count { get; }
}
