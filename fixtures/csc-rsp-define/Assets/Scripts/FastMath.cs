using UnityEngine;

public static class FastMath
{
#if USE_FAST_MATH
    public static float Distance(Vector3 a, Vector3 b)
    {
        var d = a - b;
        return Mathf.Sqrt(d.x * d.x + d.y * d.y + d.z * d.z);
    }
#else
#error USE_FAST_MATH is expected from Assets/csc.rsp
#endif
}
