using NBitcoin;

namespace WalletWasabi.WabiSabi.Client.Batching;

// Represents a payment that has been chosen for a coinjoin round.
public record InProgressPayment(IDestination Destination, Money Amount, uint256 RoundId)
	: Payment(Destination, Amount)
{
	public TxOut ToTxOut() =>
		new(Amount, Destination);

	public FinishedPayment ToFinished(uint256 txId) =>
		new(Destination, Amount, txId);

	public PendingPayment ToPending() =>
		new(Destination, Amount);
}
