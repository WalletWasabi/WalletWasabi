using NBitcoin;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Home.History.Details;

public class UnknownInputViewModel : InputViewModel
{
	public uint256 TransactionId { get; }

	public UnknownInputViewModel(uint256 transactionId)
	{
		TransactionId = transactionId;
	}
}
