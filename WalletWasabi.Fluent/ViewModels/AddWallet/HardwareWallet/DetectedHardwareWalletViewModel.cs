using System.Reactive.Disposables;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using ReactiveUI;
using WalletWasabi.Extensions;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Fluent.Models;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Logging;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.AddWallet.HardwareWallet;

[NavigationMetaData(Title = "DetectedHardwareWalletViewModel_Title")]
public partial class DetectedHardwareWalletViewModel : RoutableViewModel
{
	private DetectedHardwareWalletViewModel(WalletCreationOptions.ConnectToHardwareWallet options)
	{
		var (walletName, device) = options;

		ArgumentException.ThrowIfNullOrEmpty(walletName);
		ArgumentNullException.ThrowIfNull(device);

		WalletName = walletName;

		Type = device.WalletType;

		TypeName = device.Model.FriendlyName();

		SetupCancel(enableCancel: false, enableCancelOnEscape: false, enableCancelOnPressed: false);

		EnableBack = false;

		NextCommand = ReactiveCommand.CreateFromTask(async () => await OnNextAsync(options));

		NoCommand = ReactiveCommand.Create(OnNo);

		EnableAutoBusyOn(NextCommand);
	}

	public CancellationTokenSource? CancelCts { get; private set; }

	public string WalletName { get; }

	public WalletType Type { get; }

	public string TypeName { get; }

	public ICommand NoCommand { get; }

	private async Task OnNextAsync(WalletCreationOptions.ConnectToHardwareWallet options)
	{
		try
		{
			CancelCts ??= new CancellationTokenSource();
			var walletSettings = await UiContext.WalletRepository.NewWalletAsync(options, CancelCts.Token);
			Navigate().To().AddedWalletPage(walletSettings, options);
		}
		catch (Exception ex)
		{
			Logger.LogError(ex);
			await ShowErrorAsync(
				Lang.Resources.DetectedHardwareWalletViewModel_Title,
				ex.ToUserFriendlyString(),
				Lang.Resources.DetectedHardwareWalletViewModel_Error_AddingWallet_Caption);
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

		var enableCancel = UiContext.WalletRepository.HasWallet;
		SetupCancel(enableCancel: false, enableCancelOnEscape: enableCancel, enableCancelOnPressed: false);

		disposables.Add(Disposable.Create(() =>
		{
			CancelCts?.Cancel();
			CancelCts?.Dispose();
			CancelCts = null;
		}));
	}
}
