using NBitcoin;

namespace WalletWasabi.Blockchain.Transactions.Summary;

public class ForeignInput : IInput
{
	public Money? Amount => default;
	public bool? Confirmed => default;
}
