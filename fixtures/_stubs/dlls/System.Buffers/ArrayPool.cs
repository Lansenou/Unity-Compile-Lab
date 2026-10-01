// Original stand-in for the System.Buffers NuGet package as a project ships it in Assets/Plugins. Apache-2.0.

namespace System.Buffers
{
    /// <summary>A pool of reusable arrays.</summary>
    public abstract class ArrayPool<T>
    {
        /// <summary>The shared pool.</summary>
        public static ArrayPool<T> Shared => null;

        /// <summary>Rents an array of at least <paramref name="minimumLength"/> items.</summary>
        public abstract T[] Rent(int minimumLength);

        /// <summary>Returns a rented array.</summary>
        public abstract void Return(T[] array, bool clearArray = false);
    }
}
