using ReactiveUI;
using System.Reactive.Disposables;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Fluent.ViewModels.AddWallet;
using WalletWasabi.Fluent.ViewModels.Navigation;

namespace WalletWasabi.Fluent.ViewModels.CoinJoinProfiles;

[NavigationMetaData(Title = "CoinJoin Profiles")]
public partial class CoinJoinProfilesViewModel : RoutableViewModel
{
	public CoinJoinProfilesViewModel(KeyManager keyManager)
	{
		NextCommand = ReactiveCommand.Create(() => OnNext(keyManager));
		EnableBack = true;
	}

	private void OnNext(KeyManager keyManager)
	{
		Navigate().To(new AddedWalletPageViewModel(keyManager));
	}

	protected override void OnNavigatedTo(bool isInHistory, CompositeDisposable disposables)
	{
		base.OnNavigatedTo(isInHistory, disposables);

		var enableCancel = Services.WalletManager.HasWallet();
		SetupCancel(enableCancel: false, enableCancelOnEscape: enableCancel, enableCancelOnPressed: false);
	}
}
