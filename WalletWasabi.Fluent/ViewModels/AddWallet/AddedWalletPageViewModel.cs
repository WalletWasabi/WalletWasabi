using ReactiveUI;
using System.Linq;
using WalletWasabi.Fluent.Models.Wallets;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.AddWallet;

[NavigationMetaData(Title = "Success")]
public partial class AddedWalletPageViewModel : RoutableViewModel
{
	private readonly IWalletModel _wallet;

	private AddedWalletPageViewModel(IWalletModel wallet)
	{
		_wallet = wallet;

		WalletName = wallet.Name;
		WalletType = wallet.WalletType;

		SetupCancel(enableCancel: false, enableCancelOnEscape: false, enableCancelOnPressed: false);
		EnableBack = false;

		NextCommand = ReactiveCommand.Create(OnNext);
	}

	public WalletType WalletType { get; }

	public string WalletName { get; }

	private void OnNext()
	{
		Navigate().Clear();

		// Temporary workaround until refactoring is completed.
		MainViewModel.Instance.NavBar.SelectedWallet =
			MainViewModel.Instance.NavBar.Wallets.First(x => x.Wallet.WalletName == _wallet.Name);
	}

	protected override void OnNavigatedTo(bool isInHistory, CompositeDisposable disposables)
	{
		base.OnNavigatedTo(isInHistory, disposables);

		if (!Services.WalletManager.WalletExists(_keyManager.MasterFingerprint))
		{
			Services.WalletManager.AddWallet(_keyManager);
		}
	}
}
