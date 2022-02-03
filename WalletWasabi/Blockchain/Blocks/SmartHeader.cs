using NBitcoin;
using WalletWasabi.Exceptions;
using WalletWasabi.Helpers;

namespace WalletWasabi.Blockchain.Blocks;

public class SmartHeader
{
	public SmartHeader(uint256 blockHash, uint256 prevHash, uint height, DateTimeOffset blockTime)
		: this(blockHash, prevHash, height, blockTime.ToUnixTimeSeconds())
	{
	}

	public SmartHeader(uint256 blockHash, uint256 prevHash, uint height, long epochBlockTime)
	{
		BlockHash = Guard.NotNull(nameof(blockHash), blockHash);
		PrevHash = Guard.NotNull(nameof(prevHash), prevHash);
		if (blockHash == prevHash)
		{
			throw new InvalidOperationException($"{nameof(blockHash)} cannot be equal to {nameof(prevHash)}. Value: {blockHash}.");
		}

		Height = height;
		EpochBlockTime = epochBlockTime;
	}

	public uint256 BlockHash { get; }
	public uint256 PrevHash { get; }
	public uint Height { get; }

	/// <summary>Timestamp in seconds.</summary>
	public long EpochBlockTime { get; }

	public DateTimeOffset BlockTime => DateTimeOffset.FromUnixTimeSeconds(EpochBlockTime);

	#region SpecialHeaders

	private static SmartHeader StartingHeaderMain { get; } = new SmartHeader(
		new uint256("0000000000000000001c8018d9cb3b742ef25114f27563e3fc4a1902167f9893"),
		new uint256("000000000000000000cbeff0b533f8e1189cf09dfbebf57a8ebe349362811b80"),
		481824,
		1503539857);

	private static SmartHeader StartingHeaderTestNet { get; } = new SmartHeader(
		new uint256("00000000000f0d5edcaeba823db17f366be49a80d91d15b77747c2e017b8c20a"),
		new uint256("0000000000211a4d54bceb763ea690a4171a734c48d36f7d8e30b51d6df6ea85"),
		828575,
		1463079943);

	private static SmartHeader StartingHeaderRegTest { get; } = new SmartHeader(
		Network.RegTest.GenesisHash,
		Network.RegTest.GetGenesis().Header.HashPrevBlock,
		0,
		Network.RegTest.GetGenesis().Header.BlockTime);

	/// <summary>
	/// Where the first possible bech32 transaction ever can be found.
	/// </summary>
	public static SmartHeader GetStartingHeader(Network network)
	{
		if (network == Network.Main)
		{
			return StartingHeaderMain;
		}
		else if (network == Network.TestNet)
		{
			return StartingHeaderTestNet;
		}
		else if (network == Network.RegTest)
		{
			return StartingHeaderRegTest;
		}

		throw new NotSupportedNetworkException(network);
	}

	#endregion SpecialHeaders
}
