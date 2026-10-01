namespace Game.Core
{
    public static class HealthExtensions
    {
        // 'current' is internal to Game.Core: this compiles only because the .asmref puts this file there.
        public static bool IsDead(this Health health) => health.current <= 0;
    }
}
