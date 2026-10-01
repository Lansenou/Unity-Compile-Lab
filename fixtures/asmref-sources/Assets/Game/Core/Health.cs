using UnityEngine;

namespace Game.Core
{
    public class Health : MonoBehaviour
    {
        [SerializeField] private int max = 100;

        internal int current;

        public int Max => max;

        private void Awake()
        {
            current = max;
        }
    }
}
