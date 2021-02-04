using ReactiveUI;
using WalletWasabi.Fluent.ViewModels.Navigation;

namespace WalletWasabi.Fluent.ViewModels.TransactionBroadcasting
{
	public class SuccessBroadcastTransactionViewModel : RoutableViewModel
	{
		public SuccessBroadcastTransactionViewModel()
		{
			Title = "Success";

			NextCommand = ReactiveCommand.Create(() => Navigate().Clear());
		}
	}
}