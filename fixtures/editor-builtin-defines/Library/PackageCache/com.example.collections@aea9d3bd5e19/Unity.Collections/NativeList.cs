// Fixture source for the ucl conformance corpus. Original code, Apache-2.0.

namespace Example.Collections
{
    // Shaped like com.unity.collections: the safety handle exists only where Unity defines ENABLE_UNITY_COLLECTIONS_CHECKS
    // (the Editor and development players), and the read-only view is built differently in each case.
    public struct NativeList
    {
        private int m_Length;
#if ENABLE_UNITY_COLLECTIONS_CHECKS
        private int m_Safety;
#endif

        public ReadOnly AsReadOnly()
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            return new ReadOnly(m_Length, m_Safety);
#else
            return new ReadOnly(m_Length);
#endif
        }

        public readonly struct ReadOnly
        {
            public readonly int Length;
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            internal ReadOnly(int length, int safety) { Length = length; }
#else
            internal ReadOnly(int length) { Length = length; }
#endif
        }
    }
}
