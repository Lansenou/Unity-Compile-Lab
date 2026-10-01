using UnityEngine;
using Vendor.Contracts;

// Uses a package DLL that was compiled against System.Runtime rather than mscorlib or netstandard.
public class Leaderboard : MonoBehaviour
{
    private void Start()
    {
        var entry = new ScoreEntry { Player = "ada", Points = 42 };
        Debug.Log(entry.ToString());
    }
}
