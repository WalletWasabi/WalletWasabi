using WalletWasabi.Fluent.ViewModels.Navigation;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Advanced
{
	[NavigationMetaData(Title = "Wallet Info")]
	public partial class WalletInfoViewModel : RoutableViewModel
	{
		public WalletInfoViewModel()
		{
			SetupCancel(enableCancel: true, enableCancelOnEscape: true, enableCancelOnPressed: true);
		}
	}
}
