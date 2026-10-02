using System;
using System.Runtime.CompilerServices;

// Unity grants its collections package access to these internals.
[assembly: InternalsVisibleTo("Unity.Collections")]

namespace Unity.Collections.LowLevel.Unsafe
{
    /// <summary>The safety handle of native containers. The editor's engine build has it; release players do not.</summary>
    public struct AtomicSafetyHandle
    {
    }
}

namespace Unity.Collections
{
    using Unity.Collections.LowLevel.Unsafe;

    /// <summary>A native container (stand-in: the pointer is an IntPtr here).</summary>
    public struct NativeArray<T>
        where T : struct
    {
        /// <summary>A read-only view. The editor's engine build is compiled with ENABLE_UNITY_COLLECTIONS_CHECKS, so its
        /// internal constructor takes the safety handle.</summary>
        public struct ReadOnly
        {
            internal ReadOnly(IntPtr buffer, int length, ref AtomicSafetyHandle safety)
            {
                Length = length;
            }

            /// <summary>Element count.</summary>
            public int Length { get; }
        }
    }
}
