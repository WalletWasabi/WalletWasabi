using NBitcoin;
using WalletWasabi.Blockchain.BlockFilters;
using WalletWasabi.Blockchain.Blocks;
using Xunit;

namespace WalletWasabi.Tests.UnitTests;

public class SmartHeaderTests
{
	[Fact]
	public void ConstructorTests()
	{
		var blockTime = DateTimeOffset.UtcNow;
		new SmartHeader(uint256.Zero, uint256.One, 0, blockTime);
		new SmartHeader(uint256.Zero, uint256.One, 1, blockTime);
		new SmartHeader(uint256.Zero, uint256.One, 1, blockTime);

		Assert.Throws<ArgumentNullException>(() => new SmartHeader(blockHash: null!, uint256.One, 1, blockTime));
		Assert.Throws<ArgumentNullException>(() => new SmartHeader(uint256.Zero, header: null!, 1, blockTime));
	}

	[Fact]
	public void StartingHeaderTests()
	{
		var startingMain = SmartHeader.GetStartingHeader(Network.Main);
		var startingTest = SmartHeader.GetStartingHeader(Network.TestNet);
		var startingReg = SmartHeader.GetStartingHeader(Network.RegTest);
		var startingSig = SmartHeader.GetStartingHeader(Bitcoin.Instance.Signet);

		var expectedHashMain = new uint256("0000000000000000001c8018d9cb3b742ef25114f27563e3fc4a1902167f9893");
		var expectedHeaderMain = new uint256("000000000000000000cbeff0b533f8e1189cf09dfbebf57a8ebe349362811b80");
		uint expectedHeightMain = 481824;
		var expectedTimeMain = DateTimeOffset.FromUnixTimeSeconds(1503539857);

		var expectedHashTest = new uint256("00000000da84f2bafbbc53dee25a72ae507ff4914b867c565be350b0da8bf043");
		var expectedHeaderTest = uint256.Zero;
		uint expectedHeightTest = 0;
		var expectedTimeTest = DateTimeOffset.FromUnixTimeSeconds(1714777860);

		var expectedHashReg = Network.RegTest.GenesisHash;
		var expectedHeaderReg = uint256.Zero;
		uint expectedHeightReg = 0;
		var expectedTimeReg = Network.RegTest.GetGenesis().Header.BlockTime;

		var expectedHashSig = Bitcoin.Instance.Signet.GenesisHash;
		var expectedHeaderSig = uint256.Zero;
		uint expectedHeightSig = 0;
		var expectedTimeSig = Bitcoin.Instance.Signet.GetGenesis().Header.BlockTime;



		Assert.Equal(expectedHashMain, startingMain.BlockHash);
		Assert.Equal(expectedHeaderMain, startingMain.HeaderOrPrevBlockHash);
		Assert.Equal(expectedHeightMain, startingMain.Height);
		Assert.Equal(expectedTimeMain, startingMain.BlockTime);

		Assert.Equal(expectedHashTest, startingTest.BlockHash);
		Assert.Equal(expectedHeaderTest, startingTest.HeaderOrPrevBlockHash);
		Assert.Equal(expectedHeightTest, startingTest.Height);
		Assert.Equal(expectedTimeTest, startingTest.BlockTime);

		Assert.Equal(expectedHashReg, startingReg.BlockHash);
		Assert.Equal(expectedHeaderReg, startingReg.HeaderOrPrevBlockHash);
		Assert.Equal(expectedHeightReg, startingReg.Height);
		Assert.Equal(expectedTimeReg, startingReg.BlockTime);

		Assert.Equal(expectedHashSig, startingSig.BlockHash);
		Assert.Equal(expectedHeaderSig, startingSig.HeaderOrPrevBlockHash);
		Assert.Equal(expectedHeightSig, startingSig.Height);
		Assert.Equal(expectedTimeSig, startingSig.BlockTime);
	}
}
