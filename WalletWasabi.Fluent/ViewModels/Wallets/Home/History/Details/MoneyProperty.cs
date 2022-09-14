using NBitcoin;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Home.History.Details;

internal class MoneyProperty : Property<Money>
{
	public MoneyProperty(string title, Money value) : base(title, value)
	{
	}
}