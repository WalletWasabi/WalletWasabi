using ReactiveUI;
using WalletWasabi.Fluent.Models.Wallets;
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

		// var wallet = UiServices.WalletManager.Wallets.FirstOrDefault(x => x.WalletName == WalletName);
		// wallet?.OpenCommand.Execute(default);
	}
}
