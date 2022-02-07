using System.Linq;
using System.Text;
using NBitcoin;
using NBitcoin.Protocol;

namespace WalletWasabi.Crypto;

public record CoinJoinInputCommitmentData
{
	private byte[] _coordinatorIdentifier;
	private byte[] _roundIdentifier;

	public CoinJoinInputCommitmentData(string coordinatorIdentifier, uint256 roundIdentifier)
		: this(Encoding.ASCII.GetBytes(coordinatorIdentifier), roundIdentifier.ToBytes(false))
	{
	}

	public CoinJoinInputCommitmentData(byte[] coordinatorIdentifier, byte[] roundIdentifier)
	{
		_coordinatorIdentifier = coordinatorIdentifier;
		_roundIdentifier = roundIdentifier;
	}

	public byte[] ToBytes() =>
		new VarInt((ulong)_coordinatorIdentifier.Length).ToBytes()
			.Concat(_coordinatorIdentifier)
			.Concat(_roundIdentifier)
			.ToArray();
}
