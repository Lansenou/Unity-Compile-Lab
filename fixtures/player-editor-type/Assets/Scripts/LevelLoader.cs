using UnityEditor;
using UnityEngine;

public class LevelLoader : MonoBehaviour
{
    [SerializeField] private string levelFolder = "Assets/Levels";

    private void Start()
    {
        string[] guids = AssetDatabase.FindAssets("t:Scene", new[] { levelFolder });
        Debug.Log($"Found {guids.Length} levels in {levelFolder}");
    }
}
