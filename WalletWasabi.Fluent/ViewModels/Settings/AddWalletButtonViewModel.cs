using System.Reactive.Disposables;
using WalletWasabi.Fluent.ViewModels.AddWallet;
using WalletWasabi.Fluent.ViewModels.NavBar;

namespace WalletWasabi.Fluent.ViewModels.Settings
{
	[NavigationMetaData(Title = "Add Wallet", Searchable = false, NavBarPosition = NavBarPosition.Bottom)]
	public partial class AddWalletButtonViewModel : NavBarItemViewModel
	{
		private readonly AddWalletPageViewModel _addWalletPageViewModel;

		public AddWalletButtonViewModel(AddWalletPageViewModel addWalletPageViewModel)
		{
			Title = "Add Wallet";
			_addWalletPageViewModel = addWalletPageViewModel;
			SelectionMode = NavBarItemSelectionMode.Button;
		}

		public override string IconName => "add_regular";

		protected override void OnNavigatedTo(bool inStack, CompositeDisposable disposable)
		{
			Navigate().To(_addWalletPageViewModel);
		}
	}
}