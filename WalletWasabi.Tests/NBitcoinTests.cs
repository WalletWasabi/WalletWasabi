using NBitcoin;
using System;
using System.Collections.Generic;
using System.Text;
using Xunit;

namespace WalletWasabi.Tests
{
    public class NBitcoinTests
    {
        [Fact]
        public void DefaultPortsMatch()
        {
            Assert.Equal(Helpers.Constants.DefaultMainNetBitcoinCoreRpcPort, Network.Main.RPCPort);
            Assert.Equal(Helpers.Constants.DefaultTestNetBitcoinCoreRpcPort, Network.TestNet.RPCPort);
            Assert.Equal(Helpers.Constants.DefaultRegTestBitcoinCoreRpcPort, Network.RegTest.RPCPort);
            Assert.Equal(Helpers.Constants.DefaultMainNetBitcoinP2pPort, Network.Main.DefaultPort);
            Assert.Equal(Helpers.Constants.DefaultTestNetBitcoinP2pPort, Network.TestNet.DefaultPort);
            Assert.Equal(Helpers.Constants.DefaultRegTestBitcoinP2pPort, Network.RegTest.DefaultPort);
        }
    }
}
