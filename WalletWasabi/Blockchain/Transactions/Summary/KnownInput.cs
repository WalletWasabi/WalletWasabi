using NBitcoin;

namespace WalletWasabi.Blockchain.Transactions.Summary;

public class KnownInput : IInput
{
	public KnownInput(Money amount, bool confirmed)
	{
		Amount = amount;
		Confirmed = confirmed;
	}

	public virtual Money? Amount { get; }
	public virtual bool? Confirmed { get; }
}
