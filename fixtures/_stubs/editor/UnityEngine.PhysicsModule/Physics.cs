// Original stand-in for part of the Unity API, written for the ucl fixtures. Not Unity code. Apache-2.0.

namespace UnityEngine
{
    /// <summary>Puts an object under the control of the physics engine.</summary>
    public class Rigidbody : Component
    {
        public Vector3 linearVelocity { get => throw Native.Unavailable(); set => throw Native.Unavailable(); }

        public Vector3 angularVelocity { get => throw Native.Unavailable(); set => throw Native.Unavailable(); }

        public float mass { get => throw Native.Unavailable(); set => throw Native.Unavailable(); }

        public bool isKinematic { get => throw Native.Unavailable(); set => throw Native.Unavailable(); }

        public bool useGravity { get => throw Native.Unavailable(); set => throw Native.Unavailable(); }

        public Vector3 position { get => throw Native.Unavailable(); set => throw Native.Unavailable(); }

        public void AddForce(Vector3 force) => throw Native.Unavailable();

        public void AddForce(Vector3 force, ForceMode mode) => throw Native.Unavailable();

        public void AddTorque(Vector3 torque) => throw Native.Unavailable();

        public void MovePosition(Vector3 position) => throw Native.Unavailable();

        public void MoveRotation(Quaternion rot) => throw Native.Unavailable();
    }

    /// <summary>Base class of all colliders.</summary>
    public class Collider : Component
    {
        public bool enabled { get => throw Native.Unavailable(); set => throw Native.Unavailable(); }

        public bool isTrigger { get => throw Native.Unavailable(); set => throw Native.Unavailable(); }

        public Rigidbody attachedRigidbody => throw Native.Unavailable();

        public Vector3 ClosestPoint(Vector3 position) => throw Native.Unavailable();
    }

    /// <summary>A box-shaped collider.</summary>
    public class BoxCollider : Collider
    {
        public Vector3 center { get => throw Native.Unavailable(); set => throw Native.Unavailable(); }

        public Vector3 size { get => throw Native.Unavailable(); set => throw Native.Unavailable(); }
    }

    /// <summary>A sphere-shaped collider.</summary>
    public class SphereCollider : Collider
    {
        public Vector3 center { get => throw Native.Unavailable(); set => throw Native.Unavailable(); }

        public float radius { get => throw Native.Unavailable(); set => throw Native.Unavailable(); }
    }

    /// <summary>How a force is applied.</summary>
    public enum ForceMode
    {
        Force = 0,
        Impulse = 1,
        VelocityChange = 2,
        Acceleration = 5,
    }

    /// <summary>Information returned by a raycast.</summary>
    public struct RaycastHit
    {
        public Vector3 point => throw Native.Unavailable();

        public Vector3 normal => throw Native.Unavailable();

        public float distance => throw Native.Unavailable();

        public Collider collider => throw Native.Unavailable();

        public Transform transform => throw Native.Unavailable();

        public Rigidbody rigidbody => throw Native.Unavailable();
    }

    /// <summary>Information about a collision.</summary>
    public class Collision
    {
        public Collider collider => throw Native.Unavailable();

        public GameObject gameObject => throw Native.Unavailable();

        public Vector3 relativeVelocity => throw Native.Unavailable();
    }

    /// <summary>Global physics properties and queries.</summary>
    public static class Physics
    {
        public const int DefaultRaycastLayers = -5;

        public static Vector3 gravity { get => throw Native.Unavailable(); set => throw Native.Unavailable(); }

        public static bool Raycast(Vector3 origin, Vector3 direction) => throw Native.Unavailable();

        public static bool Raycast(Vector3 origin, Vector3 direction, float maxDistance) => throw Native.Unavailable();

        public static bool Raycast(Vector3 origin, Vector3 direction, out RaycastHit hitInfo, float maxDistance) => throw Native.Unavailable();

        public static Collider[] OverlapSphere(Vector3 position, float radius) => throw Native.Unavailable();
    }
}
