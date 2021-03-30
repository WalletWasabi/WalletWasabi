using ReactiveUI;
using WalletWasabi.Fluent.ViewModels.Navigation;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Send
{
	[NavigationMetaData(Title = "Send - Success")]
	public partial class SendSuccessViewModel : RoutableViewModel
	{
		public SendSuccessViewModel()
		{
			NextCommand = ReactiveCommand.Create(OnNext);
		}

		private void OnNext()
		{
			Navigate().Clear();
		}
	}
}
