using ReactiveUI;
using System.Linq;
using WalletWasabi.Fluent.Models.Wallets;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Wallets;
using System.Reactive.Disposables;

namespace WalletWasabi.Fluent.ViewModels.AddWallet;

[NavigationMetaData(Title = "Success")]
public partial class AddedWalletPageViewModel : RoutableViewModel
{
	private readonly IWalletSettingsModel _walletSettings;

	private AddedWalletPageViewModel(IWalletSettingsModel walletSettings)
	{
		_walletSettings = walletSettings;

		WalletName = walletSettings.WalletName;
		WalletType = walletSettings.WalletType;

		SetupCancel(enableCancel: false, enableCancelOnEscape: false, enableCancelOnPressed: false);
		EnableBack = false;

		NextCommand = ReactiveCommand.Create(OnNext);
	}

	public WalletType WalletType { get; }

	public string WalletName { get; }

	private void OnNext()
	{
		Navigate().Clear();
		UiContext.Navigate().To(_wallet);
	}

	protected override void OnNavigatedTo(bool isInHistory, CompositeDisposable disposables)
	{
		base.OnNavigatedTo(isInHistory, disposables);

		UiContext.WalletList.SaveWallet(_walletSettings);
	}
}
