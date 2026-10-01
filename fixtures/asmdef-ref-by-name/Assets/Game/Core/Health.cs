using System;
using UnityEngine;

namespace Game.Core
{
    public class Health : MonoBehaviour
    {
        [SerializeField] private int maxHealth = 100;

        public event Action Died;

        public int Current { get; private set; }

        private void Awake()
        {
            Current = maxHealth;
        }

        public void TakeDamage(int amount)
        {
            if (Current <= 0)
            {
                return;
            }

            Current = Mathf.Max(0, Current - amount);
            if (Current == 0)
            {
                Died?.Invoke();
            }
        }
    }
}
