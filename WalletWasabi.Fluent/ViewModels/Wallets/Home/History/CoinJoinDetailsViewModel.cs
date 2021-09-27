using WalletWasabi.Fluent.ViewModels.Navigation;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Home.History
{
	[NavigationMetaData(Title = "CoinJoin Details")]
	public partial class CoinJoinDetailsViewModel : RoutableViewModel
	{
		public CoinJoinDetailsViewModel()
		{
			SetupCancel(enableCancel: false, enableCancelOnEscape: true, enableCancelOnPressed: true);
			NextCommand = CancelCommand;
		}
	}
}
