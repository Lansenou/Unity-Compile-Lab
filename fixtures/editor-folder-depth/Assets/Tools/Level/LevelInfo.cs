using UnityEngine;

public class LevelInfo : MonoBehaviour
{
    [SerializeField] private string displayName = "Untitled";
    [SerializeField] private int spawnPoints = 1;

    public string DisplayName => displayName;

    public bool Validate()
    {
        return !string.IsNullOrEmpty(displayName) && spawnPoints > 0;
    }
}
