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

        public float magnitude => (float)System.Math.Sqrt((x * x) + (y * y) + (z * z));

        public float sqrMagnitude => (x * x) + (y * y) + (z * z);

        public Vector3 normalized => magnitude > 1E-05f ? this / magnitude : zero;

        public static float Distance(Vector3 a, Vector3 b) => (a - b).magnitude;

        public static float Dot(Vector3 lhs, Vector3 rhs) => (lhs.x * rhs.x) + (lhs.y * rhs.y) + (lhs.z * rhs.z);

        public static Vector3 Cross(Vector3 lhs, Vector3 rhs) => new Vector3((lhs.y * rhs.z) - (lhs.z * rhs.y), (lhs.z * rhs.x) - (lhs.x * rhs.z), (lhs.x * rhs.y) - (lhs.y * rhs.x));

        public static Vector3 Lerp(Vector3 a, Vector3 b, float t) => a + ((b - a) * Mathf.Clamp01(t));

        public static Vector3 MoveTowards(Vector3 current, Vector3 target, float maxDistanceDelta)
        {
            var delta = target - current;
            var distance = delta.magnitude;
            return distance <= maxDistanceDelta || distance == 0f ? target : current + (delta / distance * maxDistanceDelta);
        }

        public static Vector3 ClampMagnitude(Vector3 vector, float maxLength) => vector.sqrMagnitude > maxLength * maxLength ? vector.normalized * maxLength : vector;

        public static Vector3 operator +(Vector3 a, Vector3 b) => new Vector3(a.x + b.x, a.y + b.y, a.z + b.z);

        public static Vector3 operator -(Vector3 a, Vector3 b) => new Vector3(a.x - b.x, a.y - b.y, a.z - b.z);

        public static Vector3 operator -(Vector3 a) => new Vector3(-a.x, -a.y, -a.z);

        public static Vector3 operator *(Vector3 a, float d) => new Vector3(a.x * d, a.y * d, a.z * d);

        public static Vector3 operator *(float d, Vector3 a) => a * d;

        public static Vector3 operator /(Vector3 a, float d) => new Vector3(a.x / d, a.y / d, a.z / d);

        public static bool operator ==(Vector3 lhs, Vector3 rhs) => (lhs - rhs).sqrMagnitude < 9.99999944E-11f;

        public static bool operator !=(Vector3 lhs, Vector3 rhs) => !(lhs == rhs);

        public override bool Equals(object other) => other is Vector3 v && x.Equals(v.x) && y.Equals(v.y) && z.Equals(v.z);

        public override int GetHashCode() => x.GetHashCode() ^ (y.GetHashCode() << 2) ^ (z.GetHashCode() >> 2);

        public override string ToString() => string.Format(System.Globalization.CultureInfo.InvariantCulture, "({0:F2}, {1:F2}, {2:F2})", x, y, z);
    }

    /// <summary>A rotation.</summary>
    public struct Quaternion
    {
        public float x;
        public float y;
        public float z;
        public float w;

        public static Quaternion identity => throw Native.Unavailable();

        public Vector3 eulerAngles { get => throw Native.Unavailable(); set => throw Native.Unavailable(); }

        public static Quaternion Euler(float x, float y, float z) => throw Native.Unavailable();

        public static Quaternion LookRotation(Vector3 forward) => throw Native.Unavailable();

        public static Quaternion Slerp(Quaternion a, Quaternion b, float t) => throw Native.Unavailable();

        public static Vector3 operator *(Quaternion rotation, Vector3 point) => throw Native.Unavailable();

        public static Quaternion operator *(Quaternion lhs, Quaternion rhs) => throw Native.Unavailable();
    }

    /// <summary>Common math functions.</summary>
    public static class Mathf
    {
        public const float PI = 3.14159274f;

        public const float Infinity = float.PositiveInfinity;

        public const float Epsilon = 1.401298E-45f;

        public const float Deg2Rad = 0.0174532924f;

        public const float Rad2Deg = 57.29578f;

        public static float Sin(float f) => (float)System.Math.Sin(f);

        public static float Cos(float f) => (float)System.Math.Cos(f);

        public static float Sqrt(float f) => (float)System.Math.Sqrt(f);

        public static float Abs(float f) => System.Math.Abs(f);

        public static int Abs(int value) => System.Math.Abs(value);

        public static float Min(float a, float b) => a < b ? a : b;

        public static int Min(int a, int b) => a < b ? a : b;

        public static float Max(float a, float b) => a > b ? a : b;

        public static int Max(int a, int b) => a > b ? a : b;

        public static float Clamp(float value, float min, float max) => value < min ? min : value > max ? max : value;

        public static int Clamp(int value, int min, int max) => value < min ? min : value > max ? max : value;

        public static float Clamp01(float value) => Clamp(value, 0f, 1f);

        public static float Lerp(float a, float b, float t) => a + ((b - a) * Clamp01(t));

        public static float MoveTowards(float current, float target, float maxDelta) => Abs(target - current) <= maxDelta ? target : current + (System.Math.Sign(target - current) * maxDelta);

        public static int RoundToInt(float f) => (int)System.Math.Round(f);

        public static int FloorToInt(float f) => (int)System.Math.Floor(f);

        public static bool Approximately(float a, float b) => Abs(b - a) < Max(1E-06f * Max(Abs(a), Abs(b)), Epsilon * 8f);
    }
}
