using UnityEngine;

namespace Game.Runtime
{
    /// <summary>Engine-bound logic: creating GameObjects and logging need the native engine.</summary>
    public static class Spawner
    {
        public static GameObject Spawn(string name)
        {
            Debug.Log("spawning " + name);
            return new GameObject(name);
        }

        public static Vector3 Midpoint(Vector3 a, Vector3 b) => Vector3.Lerp(a, b, 0.5f);
    }
}
