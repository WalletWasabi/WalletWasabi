using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using ReactiveUI;
using WalletWasabi.Fluent.Models;
using WalletWasabi.Fluent.Models.Wallets;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Hwi.Models;
using WalletWasabi.Logging;
using WalletWasabi.Nito.AsyncEx;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.AddWallet.HardwareWallet;

[NavigationMetaData(Title = "ConnectHardwareWalletViewModel_Title")]
public partial class ConnectHardwareWalletViewModel : RoutableViewModel
{
	private readonly WalletCreationOptions.ConnectToHardwareWallet _options;
	[AutoNotify] private string _message;
	[AutoNotify] private bool _isSearching;
	[AutoNotify] private bool _existingWalletFound;
	[AutoNotify] private bool _confirmationRequired;

	private ConnectHardwareWalletViewModel(WalletCreationOptions.ConnectToHardwareWallet options)
	{
		_options = options;

		ArgumentException.ThrowIfNullOrEmpty(options.WalletName);
		_message = "";
		WalletName = options.WalletName;
		AbandonedTasks = new AbandonedTasks();
		CancelCts = new CancellationTokenSource();

		EnableBack = true;

		NextCommand = ReactiveCommand.Create(OnNext);

		NavigateToExistingWalletLoginCommand = ReactiveCommand.Create(OnNavigateToExistingWalletLogin);

		this.WhenAnyValue(x => x.Message)
			.ObserveOn(RxApp.MainThreadScheduler)
			.Subscribe(message => ConfirmationRequired = !string.IsNullOrEmpty(message));
	}

	private HwiEnumerateEntry? DetectedDevice { get; set; }

	public CancellationTokenSource CancelCts { get; set; }

	private AbandonedTasks AbandonedTasks { get; }

	public string WalletName { get; }

	public IWalletModel? ExistingWallet { get; set; }

	public ICommand NavigateToExistingWalletLoginCommand { get; }

	public WalletType Ledger => WalletType.Ledger;

	public WalletType Coldcard => WalletType.Coldcard;

	public WalletType Trezor => WalletType.Trezor;

	public WalletType Generic => WalletType.Hardware;

	private void OnNext()
	{
		if (DetectedDevice is { } device)
		{
			NavigateToNext(device);
			return;
		}

		StartDetection();
	}

	private void OnNavigateToExistingWalletLogin()
	{
		if (ExistingWallet is { })
		{
			Navigate().Clear();
			UiContext.Navigate().To(ExistingWallet);
		}
	}

	private void StartDetection()
	{
		Message = "";

		if (IsSearching)
		{
			return;
		}

		DetectedDevice = null;
		ExistingWalletFound = false;
		AbandonedTasks.AddAndClearCompleted(DetectionAsync(CancelCts.Token));
	}

	private async Task DetectionAsync(CancellationToken cancel)
	{
		IsSearching = true;

		try
		{
			var result = await UiContext.HardwareWalletInterface.DetectAsync(cancel);
			EvaluateDetectionResult(result, cancel);
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			Logger.LogError(ex);
		}
		finally
		{
			IsSearching = false;
		}
	}

	private void EvaluateDetectionResult(HwiEnumerateEntry[] devices, CancellationToken cancel)
	{
		if (devices.Length == 0)
		{
			Message = Lang.Resources.ConnectHardwareWalletViewModel_EvaluateDetection_ConnectPcEnterPin;
			return;
		}

		if (devices.Length > 1)
		{
			Message = Lang.Resources.ConnectHardwareWalletViewModel_EvaluateDetection_MakeSureOnlyOne;
			return;
		}

		var device = devices[0];

		var existingWallet = UiContext.WalletRepository.GetExistingWallet(device);
		if (existingWallet is { })
		{
			ExistingWallet = existingWallet;
			Message = Lang.Resources.ConnectHardwareWalletViewModel_EvaluateDetection_AlreadyAdded;
			ExistingWalletFound = true;
			return;
		}

		if (!device.IsInitialized())
		{
			if (device.Model == HardwareWalletModels.Coldcard)
			{
				Message = Lang.Resources.ConnectHardwareWalletViewModel_EvaluateDetection_ColdcardNotInitialized;
			}
			else
			{
				Message = Lang.Resources.ConnectHardwareWalletViewModel_EvaluateDetection_OtherNotInitialized;
				AbandonedTasks.AddAndClearCompleted(UiContext.HardwareWalletInterface.InitHardwareWalletAsync(device, cancel));
			}

			return;
		}

		if (device.Code is { })
		{
			Message = Lang.Resources.ConnectHardwareWalletViewModel_EvaluateDetection_CodeRequested;
			return;
		}

		if (device.NeedsPassphraseSent == true)
		{
			Message = Lang.Resources.ConnectHardwareWalletViewModel_EvaluateDetection_PassphraseRequested;
			return;
		}

		if (device.NeedsPinSent == true)
		{
			Message = Lang.Resources.ConnectHardwareWalletViewModel_EvaluateDetection_PinRequested;
			return;
		}

		DetectedDevice = device;

		if (!ConfirmationRequired)
		{
			NavigateToNext(DetectedDevice);
		}
	}

	private void NavigateToNext(HwiEnumerateEntry device)
	{
		Navigate().To().DetectedHardwareWallet(_options with { Device = device });
	}

	protected override void OnNavigatedTo(bool isInHistory, CompositeDisposable disposables)
	{
		base.OnNavigatedTo(isInHistory, disposables);

		var enableCancel = UiContext.WalletRepository.HasWallet;

		SetupCancel(enableCancel: enableCancel, enableCancelOnEscape: enableCancel, enableCancelOnPressed: false);

		if (isInHistory)
		{
			CancelCts = new CancellationTokenSource();
		}

		StartDetection();

		disposables.Add(Disposable.Create(async () =>
		{
			CancelCts.Cancel();
			await AbandonedTasks.WhenAllAsync();
			CancelCts.Dispose();
		}));
	}
}
