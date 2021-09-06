using System.Windows.Input;
using ReactiveUI;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.ViewModels.NavBar;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Fluent.ViewModels.Wallets;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.AddWallet.Recover
{
	[NavigationMetaData(Title = "Recover Wallet")]
	public partial class WalletAlreadyExistsViewModel : RoutableViewModel
	{
		public WalletAlreadyExistsViewModel(WalletViewModelBase walletViewModelBase)
		{
			EnableBack = true;
			WalletType = WalletHelpers.GetType(walletViewModelBase.Wallet.KeyManager);
			NextCommand = CancelCommand;

			OpenWalletCommand = ReactiveCommand.Create(() =>
			{
				if (NavigationManager.Get<NavBarViewModel>() is { } navBar)
				{
					navBar.SelectedItem = walletViewModelBase;
					Navigate().Clear();
					walletViewModelBase.OpenCommand.Execute(default);
				}
			});
		}

		public WalletType WalletType { get; }

		public ICommand OpenWalletCommand { get; }
	}
}
