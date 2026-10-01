using Game.Characters;
using UnityEngine;

namespace Game.Combat
{
    public class Weapon : MonoBehaviour
    {
        [SerializeField] private int damage = 5;

        public void Strike(Character target)
        {
            target.ApplyDamage(damage);
        }
    }
}
