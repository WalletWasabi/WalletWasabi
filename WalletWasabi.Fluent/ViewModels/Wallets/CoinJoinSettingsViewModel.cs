using WalletWasabi.Fluent.ViewModels.Navigation;

namespace WalletWasabi.Fluent.ViewModels.Wallets;

public class CoinJoinSettingsViewModel : RoutableViewModel
{
	public CoinJoinSettingsViewModel()
	{
	}

	public string Text => "Greetings";

	public override string Title
	{
		get => "Coinjoin Settings";
		protected set { }
	}
}
