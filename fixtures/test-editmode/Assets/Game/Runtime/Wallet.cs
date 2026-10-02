namespace Game.Runtime
{
    /// <summary>Pure game logic: no engine call.</summary>
    public sealed class Wallet
    {
        public int Coins { get; private set; }

        public void Earn(int amount) => Coins += amount;

        public bool Spend(int amount)
        {
            if (amount > Coins)
            {
                return false;
            }

            Coins -= amount;
            return true;
        }
    }
}
