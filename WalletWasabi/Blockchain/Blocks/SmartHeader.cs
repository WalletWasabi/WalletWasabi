using NBitcoin;
using WalletWasabi.Exceptions;
using WalletWasabi.Helpers;

namespace WalletWasabi.Blockchain.Blocks;

public record SmartHeader
{
	public SmartHeader(uint256 blockHash, uint256 header, uint height, DateTimeOffset blockTime)
		: this(blockHash, header, height, blockTime.ToUnixTimeSeconds())
	{
	}

	public SmartHeader(uint256 blockHash, uint256 header, uint height, long epochBlockTime)
	{
		BlockHash = Guard.NotNull(nameof(blockHash), blockHash);
		BlockFilterHeader = Guard.NotNull(nameof(header), header);
		Height = new ChainHeight(height);
		EpochBlockTime = epochBlockTime;
	}

	public uint256 BlockHash { get; }
	public uint256 BlockFilterHeader { get; }
	public ChainHeight Height { get; }

	/// <summary>Timestamp in seconds.</summary>
	public long EpochBlockTime { get; }

	public DateTimeOffset BlockTime => DateTimeOffset.FromUnixTimeSeconds(EpochBlockTime);

	#region SpecialHeaders

	private static SmartHeader StartingHeaderSegwitMain { get; } = new(
		new uint256("0000000000000000001c8018d9cb3b742ef25114f27563e3fc4a1902167f9893"),
		new uint256("8950b517b9246048f9fd27adeda6802e5b6d08bc7a94f628619c2d4dc4bb4d67"),
		481824,
		1503539857);

	private static SmartHeader StartingHeaderSegwitTestNet4 { get; } = new(
		Network.TestNet.GenesisHash,
		new uint256("0bf21f76e722983499fdf053df229813d79bad9e0dfd316ed3e89de2c4b7b2f1"),
		0,
		Network.TestNet.GetGenesis().Header.BlockTime);

	private static SmartHeader StartingHeaderRegTest { get; } = new(
		Network.RegTest.GenesisHash,
		new uint256("485e301e4509d7f0d954bf5b529f3ecef68c5191fd0e635f775c1d0266dc5a2b"),
		0,
		Network.RegTest.GetGenesis().Header.BlockTime);

	private static SmartHeader StartingHeaderSignet { get; } = new(
		Bitcoin.Instance.Signet.GenesisHash,
		new uint256("0d56a463c236df12c9ef21ba12f27fa17ac4bf7792a36d1636cb231f822076f4"),
		0,
		Bitcoin.Instance.Signet.GetGenesis().Header.BlockTime);

	public static SmartHeader GetStartingHeader(Network network) =>
		network.Name switch
		{
			"Main" => StartingHeaderSegwitMain,
			"TestNet4" => StartingHeaderSegwitTestNet4,
			"RegTest" => StartingHeaderRegTest,
			"signet" => StartingHeaderSignet,
			_ => throw new NotSupportedNetworkException(network)
		};

	#endregion SpecialHeaders
}
