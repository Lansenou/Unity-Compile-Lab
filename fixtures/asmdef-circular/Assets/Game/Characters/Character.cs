using Game.Combat;
using UnityEngine;

namespace Game.Characters
{
    public class Character : MonoBehaviour
    {
        [SerializeField] private Weapon equipped;

        public int Health { get; private set; } = 20;

        public void ApplyDamage(int amount)
        {
            Health -= amount;
        }

        public void Attack(Character other)
        {
            equipped.Strike(other);
        }
    }
}
