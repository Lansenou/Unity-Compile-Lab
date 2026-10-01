using UnityEditor;

namespace Game.EditorTools
{
    public static class LevelValidator
    {
        public static bool IsBusy() => EditorApplication.isCompiling;
    }
}
