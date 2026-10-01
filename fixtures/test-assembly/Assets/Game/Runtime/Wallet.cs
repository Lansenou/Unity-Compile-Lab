namespace Game.Runtime
{
    public sealed class Wallet
    {
        public int Coins { get; private set; }

        public bool Spend(int amount)
        {
            if (amount > Coins)
            {
                return false;
            }

            Coins -= amount;
            return true;
        }

        public void Earn(int amount) => Coins += amount;
    }
}
