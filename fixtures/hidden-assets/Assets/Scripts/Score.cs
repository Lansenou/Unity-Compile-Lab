using UnityEngine;

public class Score : MonoBehaviour
{
    public int Points { get; private set; }

    public void Add(int amount)
    {
        Points += amount;
    }
}
