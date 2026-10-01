using UnityEngine;

[CreateAssetMenu(fileName = "GameSettings", menuName = "Game/Settings")]
public class GameSettings : ScriptableObject
{
    [Range(0f, 1f)] public float masterVolume = 0.8f;
    public int targetFrameRate = 60;

    public void Apply()
    {
        Application.targetFrameRate = targetFrameRate;
        Debug.Log($"Applied settings for {Application.productName} (volume {Mathf.Clamp01(masterVolume)})");
    }
}
