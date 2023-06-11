using System.Linq;
using System.Reactive.Disposables;
using ReactiveUI;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.AddWallet;

[NavigationMetaData(Title = "Success")]
public partial class AddedWalletPageViewModel : RoutableViewModel
{
	private readonly KeyManager _keyManager;

	private AddedWalletPageViewModel(KeyManager keyManager)
	{
		_keyManager = keyManager;
		WalletName = _keyManager.WalletName;
		WalletType = WalletHelpers.GetType(_keyManager);

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
			MainViewModel.Instance.NavBar.Wallets.First(x => x.Wallet.WalletName == _keyManager.WalletName);
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
