using UnityEngine;

public static class ScoreService
{
    public static void Submit(int points)
    {
        Debug.Log($"Score {points}");
    }
}
