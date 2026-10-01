using UnityEngine;

public class PlayerName : MonoBehaviour
{
    private string? nickname;

    public int NameLength()
    {
        string display = nickname;
        return display.Length;
    }
}
