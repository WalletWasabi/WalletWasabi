namespace WalletWasabi.WabiSabi.Client.CoinJoinProgressEvents;

public class TransactionSigned(uint256 transactionId) : CoinJoinProgressEventArgs
{
	public uint256 TransactionId { get; } = transactionId;
}
