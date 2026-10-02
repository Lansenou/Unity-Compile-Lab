// Fixture source for the ucl conformance corpus. Original code, Apache-2.0.

namespace Unity.Collections
{
    public struct FixedList
    {
        public int Length => System.Runtime.CompilerServices.Unsafe.SizeOf<long>();
    }
}
