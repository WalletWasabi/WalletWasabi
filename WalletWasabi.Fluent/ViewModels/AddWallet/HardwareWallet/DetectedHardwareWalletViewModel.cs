using System.Reactive.Disposables;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using ReactiveUI;
using WalletWasabi.Extensions;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Helpers;
using WalletWasabi.Hwi.Models;
using WalletWasabi.Logging;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.AddWallet.HardwareWallet;

[NavigationMetaData(Title = "Hardware Wallet")]
public partial class DetectedHardwareWalletViewModel : RoutableViewModel
{
	public DetectedHardwareWalletViewModel(string walletName, HwiEnumerateEntry device)
	{
		WalletName = walletName;
		CancelCts = new CancellationTokenSource();

		Type = device.Model switch
		{
			HardwareWalletModels.Coldcard or HardwareWalletModels.Coldcard_Simulator => WalletType.Coldcard,
			HardwareWalletModels.Ledger_Nano_S or HardwareWalletModels.Ledger_Nano_X => WalletType.Ledger,
			HardwareWalletModels.Trezor_1 or HardwareWalletModels.Trezor_1_Simulator or HardwareWalletModels.Trezor_T or HardwareWalletModels.Trezor_T_Simulator => WalletType.Trezor,
			_ => WalletType.Hardware
		};

		TypeName = device.Model.FriendlyName();

		SetupCancel(enableCancel: false, enableCancelOnEscape: false, enableCancelOnPressed: false);

		EnableBack = false;

		NextCommand = ReactiveCommand.CreateFromTask(async () => await OnNextAsync(device));

		NoCommand = ReactiveCommand.Create(OnNo);

		EnableAutoBusyOn(NextCommand);
	}

	public CancellationTokenSource CancelCts { get; }

	public string WalletName { get; }

	public WalletType Type { get; }

	public string TypeName { get; }

	public ICommand NoCommand { get; }

	private async Task OnNextAsync(HwiEnumerateEntry device)
	{
		try
		{
			var walletFilePath = Services.WalletManager.WalletDirectories.GetWalletFilePaths(WalletName).walletFilePath;
			var km = await HardwareWalletOperationHelpers.GenerateWalletAsync(device, walletFilePath, Services.WalletManager.Network, CancelCts.Token);
			km.SetIcon(Type);

			Navigate().To(new AddedWalletPageViewModel(km));
		}
		catch (Exception ex)
		{
			Logger.LogError(ex);
			await ShowErrorAsync(Title, ex.ToUserFriendlyString(), "Error occurred during adding your wallet.");
			Navigate().Back();
		}
	}

	private void OnNo()
	{
		Navigate().Back();
	}

	protected override void OnNavigatedTo(bool isInHistory, CompositeDisposable disposables)
	{
		base.OnNavigatedTo(isInHistory, disposables);

		var enableCancel = Services.WalletManager.HasWallet();
		SetupCancel(enableCancel: false, enableCancelOnEscape: enableCancel, enableCancelOnPressed: false);

		disposables.Add(Disposable.Create(() =>
		{
			CancelCts.Cancel();
			CancelCts.Dispose();
		}));
	}
}
