// Original classification stand-ins for documented managed values/native bindings. Not a numerical oracle or production shim.
namespace UnityEngine
{
    public struct Color
    {
        public float r, g, b, a;
        public Color(float r, float g, float b, float a = 1f) { this.r = r; this.g = g; this.b = b; this.a = a; }
    }
    public struct Color32
    {
        public byte r, g, b, a;
        public Color32(byte r, byte g, byte b, byte a) { this.r = r; this.g = g; this.b = b; this.a = a; }
    }
    public struct Rect
    {
        public float x { get; set; }
        public float y { get; set; }
        public float width { get; set; }
        public float height { get; set; }
        public Rect(float x, float y, float width, float height) { this.x = x; this.y = y; this.width = width; this.height = height; }
    }
    public struct RectInt
    {
        public int x { get; set; }
        public int y { get; set; }
        public int width { get; set; }
        public int height { get; set; }
        public RectInt(int x, int y, int width, int height) { this.x = x; this.y = y; this.width = width; this.height = height; }
    }
    public struct Bounds
    {
        public Vector3 center { get; set; }
        public Vector3 size { get; set; }
        public Bounds(Vector3 center, Vector3 size) { this.center = center; this.size = size; }
        public bool Intersects(Bounds other) =>
            2f * System.Math.Abs(center.x - other.center.x) <= size.x + other.size.x
            && 2f * System.Math.Abs(center.y - other.center.y) <= size.y + other.size.y
            && 2f * System.Math.Abs(center.z - other.center.z) <= size.z + other.size.z;
    }
    public class Texture : Object { }
    public class Texture2D : Texture
    {
        public Texture2D(int width, int height) => throw Native.Unavailable();
    }
    public struct Matrix4x4
    {
        public float m00, m10, m20, m30, m01, m11, m21, m31, m02, m12, m22, m32, m03, m13, m23, m33;
        public static Matrix4x4 identity => new Matrix4x4 { m00 = 1f, m11 = 1f, m22 = 1f, m33 = 1f };
        public float this[int row, int column]
        {
            get
            {
                switch ((row * 4) + column)
                {
                    case 0: return m00; case 1: return m01; case 2: return m02; case 3: return m03;
                    case 4: return m10; case 5: return m11; case 6: return m12; case 7: return m13;
                    case 8: return m20; case 9: return m21; case 10: return m22; case 11: return m23;
                    case 12: return m30; case 13: return m31; case 14: return m32; case 15: return m33;
                    default: throw new System.IndexOutOfRangeException();
                }
            }
            set
            {
                switch ((row * 4) + column)
                {
                    case 0: m00 = value; break; case 1: m01 = value; break; case 2: m02 = value; break; case 3: m03 = value; break;
                    case 4: m10 = value; break; case 5: m11 = value; break; case 6: m12 = value; break; case 7: m13 = value; break;
                    case 8: m20 = value; break; case 9: m21 = value; break; case 10: m22 = value; break; case 11: m23 = value; break;
                    case 12: m30 = value; break; case 13: m31 = value; break; case 14: m32 = value; break; case 15: m33 = value; break;
                    default: throw new System.IndexOutOfRangeException();
                }
            }
        }
        public static Matrix4x4 operator *(Matrix4x4 left, Matrix4x4 right)
        {
            var result = new Matrix4x4();
            for (var row = 0; row < 4; row++)
                for (var column = 0; column < 4; column++)
                    for (var inner = 0; inner < 4; inner++)
                        result[row, column] += left[row, inner] * right[inner, column];
            return result;
        }
        public Vector3 MultiplyPoint3x4(Vector3 point) => new Vector3(
            (m00 * point.x) + (m01 * point.y) + (m02 * point.z) + m03,
            (m10 * point.x) + (m11 * point.y) + (m12 * point.z) + m13,
            (m20 * point.x) + (m21 * point.y) + (m22 * point.z) + m23);
        public static Matrix4x4 TRS(Vector3 position, Quaternion rotation, Vector3 scale) => throw Native.Unavailable();
        public Matrix4x4 inverse => throw Native.Unavailable();
    }
}
