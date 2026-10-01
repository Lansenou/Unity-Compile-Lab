using System.Threading;
using UnityEngine;

namespace Game.Threading
{
    public sealed class BackgroundWorker
    {
        private Thread thread;

        public void Start(ThreadStart work)
        {
            thread = new Thread(work) { IsBackground = true };
            thread.Start();
            Debug.Log("Background worker started");
        }
    }
}
