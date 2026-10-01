using System;

namespace Game.Shared
{
    public static class DamageCalculator
    {
        public static int Apply(int baseDamage, float multiplier, int armor)
        {
            var scaled = (int)Math.Round(baseDamage * multiplier);
            return Math.Max(0, scaled - armor);
        }
    }
}
