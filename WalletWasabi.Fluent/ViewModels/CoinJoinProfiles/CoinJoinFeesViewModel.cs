using ReactiveUI;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using System.Threading.Tasks;
using System.Windows.Input;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Fluent.ViewModels.AddWallet;
using WalletWasabi.Fluent.ViewModels.Dialogs;
using WalletWasabi.Fluent.ViewModels.Navigation;

namespace WalletWasabi.Fluent.ViewModels.CoinJoinProfiles;

[NavigationMetaData(Title = "Wallet Strategy")]
public partial class CoinJoinFeesViewModel : RoutableViewModel
{
	public CoinJoinFeesViewModel(KeyManager keyManager)
	{
		NextCommand = ReactiveCommand.Create(() => OnNext(keyManager));
		EnableBack = true;
	}

	private void OnNext(KeyManager keyManager)
	{
		Navigate().To(new CoinJoinProfilesViewModel(keyManager));
	}

	protected override void OnNavigatedTo(bool isInHistory, CompositeDisposable disposables)
	{
		base.OnNavigatedTo(isInHistory, disposables);

		var enableCancel = Services.WalletManager.HasWallet();
		SetupCancel(enableCancel: false, enableCancelOnEscape: enableCancel, enableCancelOnPressed: false);
	}
}
