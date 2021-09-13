using WalletWasabi.Fluent.ViewModels.Navigation;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Send
{
	[NavigationMetaData(Title = "Advanced")]
	public partial class AdvancedSendOptionsViewModel : RoutableViewModel
	{
		public AdvancedSendOptionsViewModel()
		{
			EnableBack = false;
			SetupCancel(enableCancel: true, enableCancelOnEscape: true, enableCancelOnPressed: true);
		}
	}
}
