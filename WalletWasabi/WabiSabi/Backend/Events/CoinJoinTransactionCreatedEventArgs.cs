using NBitcoin;
using WalletWasabi.WabiSabi.Backend.Rounds;

namespace WalletWasabi.WabiSabi.Backend.Events;

public class CoinJoinTransactionCreatedEventArgs : EventArgs
{
	public CoinJoinTransactionCreatedEventArgs(uint256 roundId, Transaction transaction) : base()
	{
		RoundId = roundId;
		Transaction = transaction;
	}

	public uint256 RoundId { get; }
	public Transaction Transaction { get; }
}
