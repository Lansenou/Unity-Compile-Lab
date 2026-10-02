// Fixture source for the ucl conformance corpus. Original code, Apache-2.0.
using System;
using Unity.Collections.LowLevel.Unsafe;

namespace Unity.Collections
{
    // Shaped like com.unity.collections' NativeList.AsReadOnly: the call matches the engine build only when the
    // compile's ENABLE_UNITY_COLLECTIONS_CHECKS agrees with the symbol the referenced engine DLL was built with.
    public struct NativeList
    {
        private int m_Length;
#if ENABLE_UNITY_COLLECTIONS_CHECKS
        private AtomicSafetyHandle m_Safety;
#endif

        public NativeArray<int>.ReadOnly AsReadOnly()
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            return new NativeArray<int>.ReadOnly(IntPtr.Zero, m_Length, ref m_Safety);
#else
            return new NativeArray<int>.ReadOnly(IntPtr.Zero, m_Length);
#endif
        }
    }
}
