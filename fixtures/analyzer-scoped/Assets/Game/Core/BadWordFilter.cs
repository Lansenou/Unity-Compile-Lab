namespace Game.Core
{
    public static class BadWordFilter
    {
        public static bool IsClean(string text) => !text.Contains("darn");
    }
}
