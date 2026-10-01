using UnityEngine;
using Vendor.Math;

namespace Game.Tweening
{
    public static class Tween
    {
        public static Vector3 Position(Vector3 from, Vector3 to, float t)
        {
            return Vector3.Lerp(from, to, Easing.OutQuad(t));
        }
    }
}
