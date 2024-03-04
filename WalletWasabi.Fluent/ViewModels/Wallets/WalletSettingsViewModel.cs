using System.Reactive.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using ReactiveUI;
using WalletWasabi.Fluent.Models.Wallets;
using WalletWasabi.Fluent.ViewModels.Navigation;

namespace WalletWasabi.Fluent.ViewModels.Wallets;

[NavigationMetaData(
	Title = "Wallet Settings",
	Caption = "Display wallet settings",
	IconName = "nav_wallet_24_regular",
	Order = 2,
	Category = "Wallet",
	Keywords = new[] { "Wallet", "Settings", },
	NavBarPosition = NavBarPosition.None,
	NavigationTarget = NavigationTarget.DialogScreen,
	Searchable = false)]
public partial class WalletSettingsViewModel : RoutableViewModel
{
	private readonly IWalletModel _wallet;
	[AutoNotify] private bool _preferPsbtWorkflow;
	[AutoNotify] private string _walletName;

	private WalletSettingsViewModel(IWalletModel wallet)
	{
		_wallet = wallet;
		_walletName = wallet.Name;
		_preferPsbtWorkflow = wallet.Settings.PreferPsbtWorkflow;
		IsHardwareWallet = wallet.IsHardwareWallet;
		IsWatchOnly = wallet.IsWatchOnlyWallet;

		SetupCancel(enableCancel: false, enableCancelOnEscape: true, enableCancelOnPressed: true);

		NextCommand = CancelCommand;

		VerifyRecoveryWordsCommand = ReactiveCommand.Create(() => Navigate().To().VerifyRecoveryWords(wallet));

		this.WhenAnyValue(x => x.PreferPsbtWorkflow)
			.Skip(1)
			.Subscribe(value =>
			{
				wallet.Settings.PreferPsbtWorkflow = value;
				wallet.Settings.Save();
			});

		this.WhenAnyValue(x => x._wallet.Name).BindTo(this, x => x.WalletName);
		
		RenameCommand = ReactiveCommand.CreateFromTask(OnRenameWalletAsync);
	}

	public ICommand RenameCommand { get; set; }

	public bool IsHardwareWallet { get; }

	public bool IsWatchOnly { get; }

	public ICommand VerifyRecoveryWordsCommand { get; }
	
	private async Task OnRenameWalletAsync()
	{
		await Navigate().To().WalletRename(_wallet).GetResultAsync();
		UiContext.WalletRepository.StoreLastSelectedWallet(_wallet);
	}
}
