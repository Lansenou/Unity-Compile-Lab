using UnityEngine;
using Vendor.Math;

namespace Game.Tweening
{
    public static class Fader
    {
        public static float Alpha(float t) => Easing.InOutCubic(Mathf.Clamp01(t));
    }
}
