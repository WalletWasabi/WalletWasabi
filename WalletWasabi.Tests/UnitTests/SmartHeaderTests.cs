using NBitcoin;
using NBitcoin.DataEncoders;
using System;
using System.Collections.Generic;
using System.Text;
using WalletWasabi.Blockchain;
using WalletWasabi.Blockchain.Blocks;
using Xunit;

namespace WalletWasabi.Tests.UnitTests
{
	public class SmartHeaderTests
	{
		// TODO
		// equality
		// comparision
		// line serialization
		// need mempool too
		[Fact]
		public void ConstructorTests()
		{
			var blockTime = DateTimeOffset.UtcNow;
			new SmartHeader(uint256.Zero, uint256.One, 0, blockTime);
			new SmartHeader(uint256.Zero, uint256.One, 1, blockTime);
			new SmartHeader(uint256.Zero, uint256.One, 1, blockTime);

			Assert.Throws<ArgumentNullException>(() => new SmartHeader(null, uint256.One, 1, blockTime));
			Assert.Throws<ArgumentNullException>(() => new SmartHeader(uint256.Zero, null, 1, blockTime));
			Assert.Throws<InvalidOperationException>(() => new SmartHeader(uint256.Zero, uint256.Zero, 1, blockTime));
		}

		[Fact]
		public void StartingHeaderTests()
		{
			var startingMain = SmartHeader.GetStartingHeader(Network.Main);
			var startingTest = SmartHeader.GetStartingHeader(Network.TestNet);
			var startingReg = SmartHeader.GetStartingHeader(Network.RegTest);

			var expectedHashMain = new uint256("0000000000000000001c8018d9cb3b742ef25114f27563e3fc4a1902167f9893");
			var expectedPrevHashMain = new uint256("000000000000000000cbeff0b533f8e1189cf09dfbebf57a8ebe349362811b80");
			uint expectedHeightMain = 481824;
			var expectedTimeMain = DateTimeOffset.FromUnixTimeSeconds(1503539857);

			var expectedHashTest = new uint256("00000000000f0d5edcaeba823db17f366be49a80d91d15b77747c2e017b8c20a");
			var expectedPrevHashTest = new uint256("0000000000211a4d54bceb763ea690a4171a734c48d36f7d8e30b51d6df6ea85");
			uint expectedHeightTest = 828575;
			var expectedTimeTest = DateTimeOffset.FromUnixTimeSeconds(1463079943);

			var expectedHashReg = Network.RegTest.GenesisHash;
			var expectedPrevHashReg = uint256.Zero;
			uint expectedHeightReg = 0;
			var expectedTimeReg = Network.RegTest.GetGenesis().Header.BlockTime;

			Assert.Equal(expectedHashMain, startingMain.BlockHash);
			Assert.Equal(expectedPrevHashMain, startingMain.PrevHash);
			Assert.Equal(expectedHeightMain, startingMain.Height);
			Assert.Equal(expectedTimeMain, startingMain.BlockTime);

			Assert.Equal(expectedHashTest, startingTest.BlockHash);
			Assert.Equal(expectedPrevHashTest, startingTest.PrevHash);
			Assert.Equal(expectedHeightTest, startingTest.Height);
			Assert.Equal(expectedTimeTest, startingTest.BlockTime);

			Assert.Equal(expectedHashReg, startingReg.BlockHash);
			Assert.Equal(expectedPrevHashReg, startingReg.PrevHash);
			Assert.Equal(expectedHeightReg, startingReg.Height);
			Assert.Equal(expectedTimeReg, startingReg.BlockTime);
		}
	}
}
