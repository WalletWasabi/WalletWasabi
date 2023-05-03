using NBitcoin;

namespace WalletWasabi.WabiSabi.Client.Batching;

// Represents a payment that has being chosen for a coinjoin round. 
public record InProgressPayment(IDestination Destination, Money Amount) 
	: Payment(Destination, Amount)
{
	public TxOut ToTxOut() =>
		new (Amount, Destination);
}
