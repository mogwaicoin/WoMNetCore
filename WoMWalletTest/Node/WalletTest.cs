﻿using WoMWallet.Node;
using Xunit;

namespace WoMWalletTest.Node
{
    public class WalletTest
    {
        [Fact]
        public void WalletTestPersist()
        {
            var wallet = new MogwaiWallet("1234", "test.dat");
            var mogwaiKeys = wallet.MogwaiKeyDict["MWG1HtzRAjZMxQDzeoFoHQbzDygGR13aWG"];
            Assert.Equal("MWG1HtzRAjZMxQDzeoFoHQbzDygGR13aWG", mogwaiKeys.Address);
            Assert.True(mogwaiKeys.HasMirrorAddress);
            Assert.Equal("MLTNLAojhmBHF3BMzG3RmzoQ1bnbnxxdeD", mogwaiKeys.MirrorAddress);
        }

        [Fact]
        public void WalletCreation()
        {
            var wallet = new MogwaiWallet();
            Assert.False(wallet.IsCreated);
            Assert.False(wallet.IsUnlocked);
        }

        [Fact]
        public void WalletUnlock()
        {
            var wallet = new MogwaiWallet("test.dat");
            Assert.True(wallet.IsCreated);
            Assert.False(wallet.IsUnlocked);
            wallet.Unlock("1234");
            Assert.True(wallet.IsUnlocked);
        }

        [Fact]
        public void WalletDeposit()
        {
            var wallet = new MogwaiWallet("test.dat");
            Assert.Null(wallet.Deposit);
            wallet.Unlock("1234");
            Assert.NotNull(wallet.Deposit);
            Assert.Equal("MBAdzUJU1zyUJLfiUDuvU8zWjenxzi7ZF6", wallet.Deposit.Address);
        }
    }
}
