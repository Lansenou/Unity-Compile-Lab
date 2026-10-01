using UnityEngine;

namespace Example.SaveGame
{
    public static class SaveSystem
    {
        public static void Save(string slot, string json)
        {
            Debug.Log($"Saved {json.Length} characters to {slot}");
        }
    }
}
