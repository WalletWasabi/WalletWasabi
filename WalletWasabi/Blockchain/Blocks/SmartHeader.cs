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
		HeaderOrPrevBlockHash = Guard.NotNull(nameof(header), header);
		Height = height;
		EpochBlockTime = epochBlockTime;
	}

	public uint256 BlockHash { get; }
	public uint256 HeaderOrPrevBlockHash { get; }
	public uint Height { get; }

	/// <summary>Timestamp in seconds.</summary>
	public long EpochBlockTime { get; }

	public DateTimeOffset BlockTime => DateTimeOffset.FromUnixTimeSeconds(EpochBlockTime);

	#region SpecialHeaders

	private static SmartHeader StartingHeaderSegwitMain { get; } = new SmartHeader(
		new uint256("0000000000000000001c8018d9cb3b742ef25114f27563e3fc4a1902167f9893"),
		new uint256("000000000000000000cbeff0b533f8e1189cf09dfbebf57a8ebe349362811b80"),
		481824,
		1503539857);

	private static SmartHeader StartingHeaderSegwitTestNet4 { get; } = new SmartHeader(
		Network.TestNet.GenesisHash,
		Network.TestNet.GetGenesis().Header.HashPrevBlock,
		0,
		Network.TestNet.GetGenesis().Header.BlockTime);

	private static SmartHeader StartingHeaderRegTest { get; } = new SmartHeader(
		Network.RegTest.GenesisHash,
		Network.RegTest.GetGenesis().Header.HashPrevBlock,
		0,
		Network.RegTest.GetGenesis().Header.BlockTime);

	private static SmartHeader StartingHeaderSignet { get; } = new SmartHeader(
		Bitcoin.Instance.Signet.GenesisHash,
		Bitcoin.Instance.Signet.GetGenesis().Header.HashPrevBlock,
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
