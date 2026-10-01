using UnityEngine;

namespace Example.Tools
{
    public sealed class Cooldown
    {
        private readonly float duration;
        private float readyAt;

        public Cooldown(float duration)
        {
            this.duration = duration;
        }

        public bool TryUse()
        {
            if (Time.time < readyAt)
            {
                return false;
            }

            readyAt = Time.time + duration;
            return true;
        }
    }
}
