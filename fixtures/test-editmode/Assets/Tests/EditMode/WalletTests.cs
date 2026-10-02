using Game.Runtime;
using NUnit.Framework;

namespace Game.Tests
{
    public class WalletTests
    {
        private Wallet wallet;

        [SetUp]
        public void CreateWallet()
        {
            wallet = new Wallet();
            wallet.Earn(10);
        }

        [Test]
        public void Spending_less_than_the_balance_succeeds() => Assert.IsTrue(wallet.Spend(4));

        [Test]
        public void Spending_more_than_the_balance_fails()
        {
            Assert.IsFalse(wallet.Spend(11));
            Assert.AreEqual(10, wallet.Coins);
        }

        [TestCase(1, 9)]
        [TestCase(5, 5)]
        [TestCase(10, 0)]
        public void Balance_after_spending(int spend, int left)
        {
            wallet.Spend(spend);
            Assert.AreEqual(left, wallet.Coins);
        }
    }
}
