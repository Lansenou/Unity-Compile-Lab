using Example.SaveGame;

namespace Game.Persistence
{
    public static class ProfileStore
    {
        public static void Store(string profileJson)
        {
#if SAVEGAME_2
            // The 2.x API is asynchronous; 1.4.0 does not have SaveAsync.
            SaveSystem.SaveAsync("profile", profileJson);
#else
            SaveSystem.Save("profile", profileJson);
#endif
        }
    }
}
