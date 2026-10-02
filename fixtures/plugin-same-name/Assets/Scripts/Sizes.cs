// Fixture source for the ucl conformance corpus. Original code, Apache-2.0.

using System.Runtime.CompilerServices;

public static class Sizes
{
    public static int Of<T>() => Unsafe.SizeOf<T>();
}
