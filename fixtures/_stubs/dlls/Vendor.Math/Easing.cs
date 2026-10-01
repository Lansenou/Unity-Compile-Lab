// Original source of a stand-in third-party plugin DLL for the ucl fixtures. Apache-2.0.

namespace Vendor.Math
{
    /// <summary>Easing curves for tweening, as a small vendor library would ship them.</summary>
    public static class Easing
    {
        /// <summary>Library version.</summary>
        public const string Version = "1.4.2";

        /// <summary>Linear interpolation.</summary>
        public static float Linear(float t) => Clamp01(t);

        /// <summary>Quadratic ease-in.</summary>
        public static float InQuad(float t)
        {
            t = Clamp01(t);
            return t * t;
        }

        /// <summary>Quadratic ease-out.</summary>
        public static float OutQuad(float t)
        {
            t = Clamp01(t);
            return t * (2f - t);
        }

        /// <summary>Cubic ease-in-out.</summary>
        public static float InOutCubic(float t)
        {
            t = Clamp01(t);
            return t < 0.5f ? 4f * t * t * t : 1f - (float)System.Math.Pow(-2f * t + 2f, 3) / 2f;
        }

        private static float Clamp01(float t) => t < 0f ? 0f : t > 1f ? 1f : t;
    }
}
