namespace Example.Inventory
{
    public sealed class InventoryGrid
    {
        private readonly string[] cells;

        public InventoryGrid(int width, int height)
        {
            cells = new string[width * height];
        }

        public int Capacity => cells.Length;
    }
}
