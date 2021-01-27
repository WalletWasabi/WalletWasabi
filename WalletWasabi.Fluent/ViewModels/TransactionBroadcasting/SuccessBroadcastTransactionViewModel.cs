using ReactiveUI;
using WalletWasabi.Fluent.ViewModels.Navigation;

namespace WalletWasabi.Fluent.ViewModels.TransactionBroadcasting
{
	[NavigationMetaData(Title = "Success")]
	public partial class SuccessBroadcastTransactionViewModel : RoutableViewModel
	{
		public SuccessBroadcastTransactionViewModel()
		{
			NextCommand = ReactiveCommand.Create(() => Navigate().Clear());
		}
	}
}