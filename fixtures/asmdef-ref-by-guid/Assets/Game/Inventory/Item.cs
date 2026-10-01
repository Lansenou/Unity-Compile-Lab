using UnityEngine;

namespace Game.Inventory
{
    [CreateAssetMenu(menuName = "Game/Item")]
    public class Item : ScriptableObject
    {
        [SerializeField] private string displayName = "Item";
        [SerializeField] private int maxStack = 1;

        public string DisplayName => displayName;

        public int MaxStack => maxStack;
    }
}
