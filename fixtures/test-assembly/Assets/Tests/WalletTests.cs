using Game.Runtime;

namespace Game.Tests
{
    public static class WalletTests
    {
        public static bool SpendMoreThanBalanceFails()
        {
            var wallet = new Wallet();
            wallet.Earn(5);
            return !wallet.Spend(10) && wallet.Coins == 5;
        }
    }
}
