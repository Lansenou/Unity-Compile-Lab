using UnityEngine;

public class BadScore : MonoBehaviour
{
    public int Value { get; private set; }

    public void Add(int points)
    {
        Value += points;
    }
}
