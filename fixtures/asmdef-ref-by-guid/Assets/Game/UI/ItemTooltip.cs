using Game.Inventory;
using UnityEngine;

namespace Game.UI
{
    public class ItemTooltip : MonoBehaviour
    {
        [SerializeField] private Item item;

        public string Describe()
        {
            return item == null ? string.Empty : $"{item.DisplayName} (stacks to {item.MaxStack})";
        }
    }
}
