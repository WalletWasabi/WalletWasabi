using WalletWasabi.Fluent.ViewModels.Navigation;

namespace WalletWasabi.Fluent.ViewModels.TransactionBroadcaster
{
	public class TransactionBroadcasterViewModel : RoutableViewModel
	{
		public TransactionBroadcasterViewModel()
		{

		}

		public override NavigationTarget DefaultTarget => NavigationTarget.DialogScreen;
	}
}