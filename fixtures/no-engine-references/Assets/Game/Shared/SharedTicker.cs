namespace Game.Shared
{
    public class SharedTicker : MonoBehaviour
    {
        public int Ticks { get; private set; }

        private void Update()
        {
            Ticks++;
        }
    }
}
