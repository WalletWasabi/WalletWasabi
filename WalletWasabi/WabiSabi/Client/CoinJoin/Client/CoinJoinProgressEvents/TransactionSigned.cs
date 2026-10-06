namespace WalletWasabi.WabiSabi.Client.CoinJoinProgressEvents;

public class TransactionSigned(uint256 transactionId, ImmutableArray<OutPoint> inputs) : CoinJoinProgressEventArgs
{
	public uint256 TransactionId { get; } = transactionId;
	public ImmutableArray<OutPoint> Inputs { get; } = inputs;
}
