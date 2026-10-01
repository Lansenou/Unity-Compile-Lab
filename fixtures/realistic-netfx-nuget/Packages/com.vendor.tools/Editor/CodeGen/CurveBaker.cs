using Vendor.Math;

namespace Vendor.Tools.CodeGen
{
    // A code-generation helper of a package. Its asmdef also lists Unity.IL2CPP.dll, which the Editor install
    // ships outside the folders the Editor offers as precompiled references: the Editor skips the entry silently.
    public static class CurveBaker
    {
        public static float Bake(float t) => Easing.InQuad(t);
    }
}
