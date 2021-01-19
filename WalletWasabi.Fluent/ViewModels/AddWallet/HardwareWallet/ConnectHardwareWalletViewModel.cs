using System.IO;
using System.Reactive.Disposables;
using System.Threading.Tasks;
using System.Timers;
using System.Windows.Input;
using ReactiveUI;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Hwi.Models;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.AddWallet.HardwareWallet
{
	public partial class ConnectHardwareWalletViewModel : RoutableViewModel
	{
		[AutoNotify] private string _message;
		[AutoNotify] private bool _isSearching;

		public ConnectHardwareWalletViewModel(string walletName, WalletManager walletManager)
		{
			Title = "Hardware Wallet";
			_message = "";
			WalletName = walletName;
			WalletManager = walletManager;
			HardwareWalletOperations = new HardwareWalletOperations(walletManager);

			NextCommand = ReactiveCommand.Create(RunDetection);

			// TODO: Create an up-to-date article
			OpenBrowserCommand = ReactiveCommand.CreateFromTask(async () =>
				await IoHelpers.OpenBrowserAsync("https://docs.wasabiwallet.io/using-wasabi/ColdWasabi.html#using-hardware-wallet-step-by-step"));
		}

		public string WalletName { get; }

		public WalletManager WalletManager { get; }

		public HardwareWalletOperations HardwareWalletOperations { get; }

		public ICommand OpenBrowserCommand { get; }

		private void RunDetection()
		{
			IsSearching = true;
			Message = "";
			HardwareWalletOperations.StartDetection();
		}

		private void OnPassphraseNeeded(object sender, ElapsedEventArgs e)
		{
			IsSearching = false;
			Message = "Check your device and enter your passphrase.";
		}

		private void OnDetectionCompleted(object? sender, HwiEnumerateEntry[] devices)
		{
			IsSearching = false;

			if (devices.Length == 0)
			{
				Message = "Connect your wallet to the USB port on your PC / Enter the PIN on the Wallet.";
				return;
			}

			if (devices.Length > 1)
			{
				Message = "Make sure you have only one hardware wallet connected to the PC.";
				return;
			}

			var device = devices[0];

			if (!device.IsInitialized())
			{
				if (device.Model == HardwareWalletModels.Coldcard)
				{
					Message = "Initialize your device first.";
				}
				else
				{
					Message = "Check your device and finish the initialization.";
					Task.Run(() => HardwareWalletOperations.InitHardwareWalletAsync(device));
				}

				return;
			}

			if (device.Code is { })
			{
				Message = "Something happened with your device, unlock it with your PIN/Passphrase or reconnect to the PC.";
				return;
			}

			if (device.NeedsPassphraseSent == true)
			{
				Message = "Enter your passphrase on your device.";
				return;
			}

			if (device.NeedsPinSent == true)
			{
				Message = "Enter your PIN on your device.";
				return;
			}

			Navigate().To(new DetectedHardwareWalletViewModel(WalletManager, WalletName, device));
		}

		protected override void OnNavigatedTo(bool inStack, CompositeDisposable disposable)
		{
			base.OnNavigatedTo(inStack, disposable);

			HardwareWalletOperations.DetectionCompleted += OnDetectionCompleted;
			HardwareWalletOperations.PassphraseTimer.Elapsed += OnPassphraseNeeded;

			RunDetection();

			Disposable.Create(() =>
			{
				HardwareWalletOperations.DetectionCompleted -= OnDetectionCompleted;
				HardwareWalletOperations.PassphraseTimer.Elapsed -= OnPassphraseNeeded;
				HardwareWalletOperations.Dispose();
			})
			.DisposeWith(disposable);
		}
	}
}
