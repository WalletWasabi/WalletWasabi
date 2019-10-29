using NBitcoin;
using System;
using System.Collections.Generic;
using System.Text;
using Xunit;

namespace WalletWasabi.Tests.UnitTests
{
	public class NBitcoinTests
	{
		[Fact]
		public void DefaultPortsMatch()
		{
			Assert.Equal(WalletWasabi.Helpers.Constants.DefaultMainNetBitcoinCoreRpcPort, Network.Main.RPCPort);
			Assert.Equal(WalletWasabi.Helpers.Constants.DefaultTestNetBitcoinCoreRpcPort, Network.TestNet.RPCPort);
			Assert.Equal(WalletWasabi.Helpers.Constants.DefaultRegTestBitcoinCoreRpcPort, Network.RegTest.RPCPort);
			Assert.Equal(WalletWasabi.Helpers.Constants.DefaultMainNetBitcoinP2pPort, Network.Main.DefaultPort);
			Assert.Equal(WalletWasabi.Helpers.Constants.DefaultTestNetBitcoinP2pPort, Network.TestNet.DefaultPort);
			Assert.Equal(WalletWasabi.Helpers.Constants.DefaultRegTestBitcoinP2pPort, Network.RegTest.DefaultPort);
		}
	}
}
