using NUnit.Framework;
using UnityEngine.Rendering;

namespace Game.Tests
{
    // CommandBuffer has a finalizer that releases its native buffer. Outside Unity the constructor fails (an engine
    // call) after the object exists, so the finalizer later runs on a buffer that was never created and throws on
    // the finalizer thread: an unhandled exception that ends the process.
    public class RenderTests
    {
        [Test, Order(1)]
        public void A_pure_case_before() => Assert.AreEqual(4, 2 + 2);

        // Constructs the engine type directly: classified needs-unity before anything runs.
        [Test, Order(2)]
        public void B_creates_a_command_buffer()
        {
            var buffer = new CommandBuffer();
            buffer.name = "blit";
        }

        // Constructs it through a generic pool: not visible statically, so it runs and leaves a pending finalizer.
        [Test, Order(3)]
        public void C_pooled_command_buffer() => Assert.IsNotNull(new Pool<CommandBuffer>().Get());

        // Runs the pending finalizers: the host process dies here.
        [Test, Order(4)]
        public void D_collects_garbage()
        {
            System.GC.Collect();
            System.GC.WaitForPendingFinalizers();
        }

        [Test, Order(5)]
        public void E_pure_case_after() => Assert.AreEqual("ab", "a" + "b");
    }
}
