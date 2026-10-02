namespace Game.Tests
{
    /// <summary>A minimal object pool: new T() compiles to Activator.CreateInstance, so the construction is not visible in the caller's IL.</summary>
    public sealed class Pool<T> where T : new()
    {
        public T Get() => new T();
    }
}
