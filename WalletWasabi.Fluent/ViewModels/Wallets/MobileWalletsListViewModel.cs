using System.Collections.ObjectModel;
using WalletWasabi.Fluent.Infrastructure;
using WalletWasabi.Fluent.ViewModels.NavBar;
using WalletWasabi.Fluent.ViewModels.Navigation;

namespace WalletWasabi.Fluent.ViewModels.Wallets;

[AppLifetime]
public partial class MobileWalletsListViewModel : RoutableViewModel
{
	private string _title = "Wallets";

	public MobileWalletsListViewModel(UiContext uiContext) : base(uiContext)
	{
	}

	public override string Title
	{
		get => _title;
		protected set => this.RaiseAndSetIfChanged(ref _title, value);
	}

	public ReadOnlyObservableCollection<WalletPageViewModel> Wallets => UiContext.MainViewModel!.NavBar.Wallets;

	public void SelectWallet(WalletPageViewModel wallet)
	{
		UiContext.MainViewModel!.NavBar.SelectedWallet = wallet;
	}
}
