using NBitcoin;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Home.History.Details;

public class KnownInputViewModel : InputViewModel
{
	public Money Amount { get; }
	public string Address { get; }

	public KnownInputViewModel(Money amount, string address)
	{
		Amount = amount;
		Address = address;
	}
}
