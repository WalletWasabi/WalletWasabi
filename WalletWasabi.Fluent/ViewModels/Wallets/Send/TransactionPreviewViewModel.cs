using WalletWasabi.Fluent.ViewModels.Navigation;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Send
{
	[NavigationMetaData(Title = "Transaction Preview")]
	public partial class TransactionPreviewViewModel : RoutableViewModel
	{
		private readonly WalletViewModel _owner;

		public TransactionPreviewViewModel(WalletViewModel walletViewModel)
		{
			_owner = walletViewModel;
		}
	}
}