// Original stand-in for part of the Unity API, written for the ucl fixtures. Not Unity code. Apache-2.0.

namespace UnityEngine
{
    /// <summary>A 3D vector.</summary>
    public struct Vector3
    {
        public float x;
        public float y;
        public float z;

        public Vector3(float x, float y, float z)
        {
            this.x = x;
            this.y = y;
            this.z = z;
        }

        public Vector3(float x, float y)
        {
            this.x = x;
            this.y = y;
            z = 0f;
        }

        public static Vector3 zero => default;

        public static Vector3 one => new Vector3(1f, 1f, 1f);

        public static Vector3 up => new Vector3(0f, 1f, 0f);

        public static Vector3 down => new Vector3(0f, -1f, 0f);

        public static Vector3 forward => new Vector3(0f, 0f, 1f);

        public static Vector3 back => new Vector3(0f, 0f, -1f);

        public static Vector3 right => new Vector3(1f, 0f, 0f);

        public static Vector3 left => new Vector3(-1f, 0f, 0f);

        public float magnitude => throw null;

        public float sqrMagnitude => throw null;

        public Vector3 normalized => throw null;

        public static float Distance(Vector3 a, Vector3 b) => throw null;

        public static float Dot(Vector3 lhs, Vector3 rhs) => throw null;

        public static Vector3 Cross(Vector3 lhs, Vector3 rhs) => throw null;

        public static Vector3 Lerp(Vector3 a, Vector3 b, float t) => throw null;

        public static Vector3 MoveTowards(Vector3 current, Vector3 target, float maxDistanceDelta) => throw null;

        public static Vector3 ClampMagnitude(Vector3 vector, float maxLength) => throw null;

        public static Vector3 operator +(Vector3 a, Vector3 b) => throw null;

        public static Vector3 operator -(Vector3 a, Vector3 b) => throw null;

        public static Vector3 operator -(Vector3 a) => throw null;

        public static Vector3 operator *(Vector3 a, float d) => throw null;

        public static Vector3 operator *(float d, Vector3 a) => throw null;

        public static Vector3 operator /(Vector3 a, float d) => throw null;

        public static bool operator ==(Vector3 lhs, Vector3 rhs) => throw null;

        public static bool operator !=(Vector3 lhs, Vector3 rhs) => throw null;

        public override bool Equals(object other) => throw null;

        public override int GetHashCode() => throw null;

        public override string ToString() => throw null;
    }

    /// <summary>A rotation.</summary>
    public struct Quaternion
    {
        public float x;
        public float y;
        public float z;
        public float w;

        public static Quaternion identity => throw null;

        public Vector3 eulerAngles { get => throw null; set => throw null; }

        public static Quaternion Euler(float x, float y, float z) => throw null;

        public static Quaternion LookRotation(Vector3 forward) => throw null;

        public static Quaternion Slerp(Quaternion a, Quaternion b, float t) => throw null;

        public static Vector3 operator *(Quaternion rotation, Vector3 point) => throw null;

        public static Quaternion operator *(Quaternion lhs, Quaternion rhs) => throw null;
    }

    /// <summary>Common math functions.</summary>
    public static class Mathf
    {
        public const float PI = 3.14159274f;

        public const float Infinity = float.PositiveInfinity;

        public const float Epsilon = 1.401298E-45f;

        public const float Deg2Rad = 0.0174532924f;

        public const float Rad2Deg = 57.29578f;

        public static float Sin(float f) => throw null;

        public static float Cos(float f) => throw null;

        public static float Sqrt(float f) => throw null;

        public static float Abs(float f) => throw null;

        public static int Abs(int value) => throw null;

        public static float Min(float a, float b) => throw null;

        public static int Min(int a, int b) => throw null;

        public static float Max(float a, float b) => throw null;

        public static int Max(int a, int b) => throw null;

        public static float Clamp(float value, float min, float max) => throw null;

        public static int Clamp(int value, int min, int max) => throw null;

        public static float Clamp01(float value) => throw null;

        public static float Lerp(float a, float b, float t) => throw null;

        public static float MoveTowards(float current, float target, float maxDelta) => throw null;

        public static int RoundToInt(float f) => throw null;

        public static int FloorToInt(float f) => throw null;

        public static bool Approximately(float a, float b) => throw null;
    }
}
