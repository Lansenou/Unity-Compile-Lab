// Original stand-in for part of the Unity API, written for the ucl fixtures. Not Unity code. Apache-2.0.

namespace UnityEngine.Rendering
{
    /// <summary>
    /// List of graphics commands to execute. Native-backed, with a finalizer that releases the native buffer, as in
    /// Unity: Finalize calls Dispose(false), which calls ReleaseBuffer, whose binding throws NullReferenceException
    /// when the native pointer is null (a constructor that failed outside Unity leaves it null).
    /// </summary>
    public class CommandBuffer : System.IDisposable
    {
        internal System.IntPtr m_Ptr;

        public CommandBuffer()
        {
            m_Ptr = InitBuffer();
        }

        ~CommandBuffer()
        {
            Dispose(false);
        }

        public string name { get => throw Native.Unavailable(); set => throw Native.Unavailable(); }

        public void Clear() => throw Native.Unavailable();

        public void Dispose()
        {
            Dispose(true);
            System.GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            ReleaseBuffer();
            m_Ptr = System.IntPtr.Zero;
        }

        private static System.IntPtr InitBuffer() => throw Native.Unavailable();

        private void ReleaseBuffer()
        {
            if (m_Ptr == System.IntPtr.Zero)
            {
                throw new System.NullReferenceException("Object reference not set to an instance of an object.");
            }

            throw Native.Unavailable();
        }
    }
}
