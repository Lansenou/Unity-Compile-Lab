using UnityEngine;

namespace Game.Tools
{
    public class BadFrameCounter : MonoBehaviour
    {
        private int slowFrames;

        private void Update()
        {
            if (Time.deltaTime > 0.05f)
            {
                slowFrames++;
            }
        }
    }
}
