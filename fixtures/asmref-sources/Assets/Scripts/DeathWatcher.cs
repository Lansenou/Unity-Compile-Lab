using Game.Core;
using UnityEngine;

public class DeathWatcher : MonoBehaviour
{
    [SerializeField] private Health health;

    private void Update()
    {
        if (health.IsDead())
        {
            Debug.Log("Game over");
        }
    }
}
