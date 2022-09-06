using NBitcoin;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Home.History.Details;

public interface ITransferredAmount
{
	public Money Amount { get; }
	public string Address { get; }
	public bool IsSpent { get; }
}
