// Original stand-in for the System.Memory NuGet package as a project ships it in Assets/Plugins. Apache-2.0.
// Only the members the fixtures use; the real package declares many more.

namespace System
{
    /// <summary>A contiguous region of memory.</summary>
    public readonly ref struct Span<T>
    {
        /// <summary>Wraps an array.</summary>
        public Span(T[] array) { }

        /// <summary>Number of items.</summary>
        public int Length => 0;
    }

    /// <summary>A read-only contiguous region of memory.</summary>
    public readonly ref struct ReadOnlySpan<T>
    {
        /// <summary>Number of items.</summary>
        public int Length => 0;
    }

    /// <summary>Extension methods for spans.</summary>
    public static class MemoryExtensions
    {
        /// <summary>Spans an array.</summary>
        public static Span<T> AsSpan<T>(this T[] array) => new Span<T>(array);

        /// <summary>Spans a string.</summary>
        public static ReadOnlySpan<char> AsSpan(this string text) => default;
    }
}
