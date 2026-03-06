using NBitcoin;
using WalletWasabi.Blockchain.BlockFilters;
using WalletWasabi.Blockchain.Blocks;
using WalletWasabi.Models;
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
		var startingMain = FilterCheckpoints.GetWasabiGenesisFilter(Network.Main).Header;
		var startingTest = FilterCheckpoints.GetWasabiGenesisFilter(Network.TestNet).Header;
		var startingReg = FilterCheckpoints.GetWasabiGenesisFilter(Network.RegTest).Header;
		var startingSig = FilterCheckpoints.GetWasabiGenesisFilter(Bitcoin.Instance.Signet).Header;

		var expectedHashMain = new uint256("0000000000000000001c8018d9cb3b742ef25114f27563e3fc4a1902167f9893");
		var expectedHeaderMain = new uint256("8950b517b9246048f9fd27adeda6802e5b6d08bc7a94f628619c2d4dc4bb4d67");
		var expectedHeightMain = new Height.ChainHeight(481824);
		var expectedTimeMain = DateTimeOffset.FromUnixTimeSeconds(1503539857);

		var expectedHashTest = new uint256("00000000da84f2bafbbc53dee25a72ae507ff4914b867c565be350b0da8bf043");
		var expectedHeaderTest = new uint256("0bf21f76e722983499fdf053df229813d79bad9e0dfd316ed3e89de2c4b7b2f1");
		var expectedHeightTest = new Height.ChainHeight(0);
		var expectedTimeTest = DateTimeOffset.FromUnixTimeSeconds(1714777860);

		var expectedHashReg = Network.RegTest.GenesisHash;
		var expectedHeaderReg = new uint256("485e301e4509d7f0d954bf5b529f3ecef68c5191fd0e635f775c1d0266dc5a2b");
		var expectedHeightReg = new Height.ChainHeight(0);
		var expectedTimeReg = Network.RegTest.GetGenesis().Header.BlockTime;

		var expectedHashSig = Bitcoin.Instance.Signet.GenesisHash;
		var expectedHeaderSig = new uint256("0d56a463c236df12c9ef21ba12f27fa17ac4bf7792a36d1636cb231f822076f4");
		var expectedHeightSig = new Height.ChainHeight(0);
		var expectedTimeSig = Bitcoin.Instance.Signet.GetGenesis().Header.BlockTime;

		Assert.Equal(expectedHashMain, startingMain.BlockHash);
		Assert.Equal(expectedHeaderMain, startingMain.BlockFilterHeader);
		Assert.Equal(expectedHeightMain, startingMain.Height);
		Assert.Equal(expectedTimeMain, startingMain.BlockTime);

		Assert.Equal(expectedHashTest, startingTest.BlockHash);
		Assert.Equal(expectedHeaderTest, startingTest.BlockFilterHeader);
		Assert.Equal(expectedHeightTest, startingTest.Height);
		Assert.Equal(expectedTimeTest, startingTest.BlockTime);

		Assert.Equal(expectedHashReg, startingReg.BlockHash);
		Assert.Equal(expectedHeaderReg, startingReg.BlockFilterHeader);
		Assert.Equal(expectedHeightReg, startingReg.Height);
		Assert.Equal(expectedTimeReg, startingReg.BlockTime);

		Assert.Equal(expectedHashSig, startingSig.BlockHash);
		Assert.Equal(expectedHeaderSig, startingSig.BlockFilterHeader);
		Assert.Equal(expectedHeightSig, startingSig.Height);
		Assert.Equal(expectedTimeSig, startingSig.BlockTime);
	}
}
