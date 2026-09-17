using System.Collections.ObjectModel;
using System.Windows.Input;
using WalletWasabi.Fluent.Infrastructure;
using WalletWasabi.Fluent.ViewModels.Navigation;

namespace WalletWasabi.Fluent.ViewModels.Wallets;

[AppLifetime]
public partial class MobileWalletsListViewModel : RoutableViewModel
{
	private string _title = "Wallets";

	public MobileWalletsListViewModel(UiContext uiContext) : base(uiContext)
	{
		SelectWalletCommand = ReactiveCommand.Create<WalletPageViewModel>(SelectWallet);
	}

	public override string Title { get => _title; protected set => this.RaiseAndSetIfChanged(ref _title, value); }
	public ReadOnlyObservableCollection<WalletPageViewModel> Wallets => UiContext.MainViewModel!.NavBar.Wallets;
	public ICommand SelectWalletCommand { get; }

	public void SelectWallet(WalletPageViewModel wallet)
	{
		var main = UiContext.MainViewModel;
		if (main is null || !Wallets.Contains(wallet)) return;
		if (ReferenceEquals(main.NavBar.SelectedWallet, wallet))
		{
			// Reassigning SelectedWallet emits no change for the same object. Return
			// explicitly to its existing dashboard, loading screen, or login route.
			if (wallet.CurrentPage is { } page)
				UiContext.Navigate().To(page, NavigationTarget.HomeScreen, NavigationMode.Clear);
		}
		else main.NavBar.SelectedWallet = wallet;
	}
}
