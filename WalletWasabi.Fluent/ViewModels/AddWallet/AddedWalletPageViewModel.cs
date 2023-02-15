using System.Linq;
using System.Reactive.Disposables;
using ReactiveUI;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.ViewModels.NavBar;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.AddWallet;

[NavigationMetaData(Title = "Success")]
public partial class AddedWalletPageViewModel : RoutableViewModel
{
	private readonly KeyManager _keyManager;

	public AddedWalletPageViewModel(KeyManager keyManager)
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

		var wallet = UiServices.WalletManager.Wallets.FirstOrDefault(x => x.WalletName == WalletName);
		wallet?.OpenCommand.Execute(default);
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
