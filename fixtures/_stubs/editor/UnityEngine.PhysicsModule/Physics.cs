// Original stand-in for part of the Unity API, written for the ucl fixtures. Not Unity code. Apache-2.0.

namespace UnityEngine
{
    /// <summary>Puts an object under the control of the physics engine.</summary>
    public class Rigidbody : Component
    {
        public Vector3 linearVelocity { get => throw null; set => throw null; }

        public Vector3 angularVelocity { get => throw null; set => throw null; }

        public float mass { get => throw null; set => throw null; }

        public bool isKinematic { get => throw null; set => throw null; }

        public bool useGravity { get => throw null; set => throw null; }

        public Vector3 position { get => throw null; set => throw null; }

        public void AddForce(Vector3 force) => throw null;

        public void AddForce(Vector3 force, ForceMode mode) => throw null;

        public void AddTorque(Vector3 torque) => throw null;

        public void MovePosition(Vector3 position) => throw null;

        public void MoveRotation(Quaternion rot) => throw null;
    }

    /// <summary>Base class of all colliders.</summary>
    public class Collider : Component
    {
        public bool enabled { get => throw null; set => throw null; }

        public bool isTrigger { get => throw null; set => throw null; }

        public Rigidbody attachedRigidbody => throw null;

        public Vector3 ClosestPoint(Vector3 position) => throw null;
    }

    /// <summary>A box-shaped collider.</summary>
    public class BoxCollider : Collider
    {
        public Vector3 center { get => throw null; set => throw null; }

        public Vector3 size { get => throw null; set => throw null; }
    }

    /// <summary>A sphere-shaped collider.</summary>
    public class SphereCollider : Collider
    {
        public Vector3 center { get => throw null; set => throw null; }

        public float radius { get => throw null; set => throw null; }
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
        public Vector3 point => throw null;

        public Vector3 normal => throw null;

        public float distance => throw null;

        public Collider collider => throw null;

        public Transform transform => throw null;

        public Rigidbody rigidbody => throw null;
    }

    /// <summary>Information about a collision.</summary>
    public class Collision
    {
        public Collider collider => throw null;

        public GameObject gameObject => throw null;

        public Vector3 relativeVelocity => throw null;
    }

    /// <summary>Global physics properties and queries.</summary>
    public static class Physics
    {
        public const int DefaultRaycastLayers = -5;

        public static Vector3 gravity { get => throw null; set => throw null; }

        public static bool Raycast(Vector3 origin, Vector3 direction) => throw null;

        public static bool Raycast(Vector3 origin, Vector3 direction, float maxDistance) => throw null;

        public static bool Raycast(Vector3 origin, Vector3 direction, out RaycastHit hitInfo, float maxDistance) => throw null;

        public static Collider[] OverlapSphere(Vector3 position, float radius) => throw null;
    }
}
