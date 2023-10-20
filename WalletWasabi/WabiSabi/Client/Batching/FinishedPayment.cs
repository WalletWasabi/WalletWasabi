using NBitcoin;

namespace WalletWasabi.WabiSabi.Client.Batching;

// Represents a payment that was made successfully in a coinjoin transaction.
public record FinishedPayment(IDestination Destination, Money Amount, uint256 TransactionId) 
	: Payment(Destination, Amount)
{
}
