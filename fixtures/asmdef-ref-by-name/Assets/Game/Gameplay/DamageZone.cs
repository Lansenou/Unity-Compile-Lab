using Game.Core;
using UnityEngine;

namespace Game.Gameplay
{
    public class DamageZone : MonoBehaviour
    {
        [SerializeField] private int damagePerHit = 10;

        public void Hit(GameObject target)
        {
            if (target.TryGetComponent(out Health health))
            {
                health.TakeDamage(damagePerHit);
            }
        }
    }
}
