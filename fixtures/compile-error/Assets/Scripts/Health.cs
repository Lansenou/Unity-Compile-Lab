using UnityEngine;

public class Health : MonoBehaviour
{
    [SerializeField] private int maxHealth = 3;

    public int Current { get; private set; }

    private void Awake()
    {
        Current = maxHealth;
    }

    public void Damage(int amount)
    {
        Current = Mathf.Max(0, Current - amount);
    }
}
