using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reactive.Disposables;
using System.Threading.Tasks;
using System.Timers;
using System.Windows.Input;
using ReactiveUI;
using WalletWasabi.Fluent.ViewModels.Login;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Fluent.ViewModels.Wallets;
using WalletWasabi.Hwi.Models;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.AddWallet.HardwareWallet
{
	public partial class ConnectHardwareWalletViewModel : RoutableViewModel
	{
		[AutoNotify] private string _message;
		[AutoNotify] private bool _isSearching;
		[AutoNotify] private bool _existingWalletFound;

		public ConnectHardwareWalletViewModel(string walletName, WalletManager walletManager, ObservableCollection<WalletViewModelBase> wallets)
		{
			Title = "Hardware Wallet";
			_message = "";
			WalletName = walletName;
			WalletManager = walletManager;
			Wallets = wallets;
			HardwareWalletOperations = new HardwareWalletOperations(walletManager);

			NextCommand = ReactiveCommand.Create(RunDetection);

			// TODO: Create an up-to-date article
			OpenBrowserCommand = ReactiveCommand.CreateFromTask(async () =>
				await IoHelpers.OpenBrowserAsync("https://docs.wasabiwallet.io/using-wasabi/ColdWasabi.html#using-hardware-wallet-step-by-step"));

			NavigateToExistingWalletLoginCommand = ReactiveCommand.Create(() =>
			{
				if (ExistingWallet is null)
				{
					return;
				}

				Navigate().Clear();
				Navigate(NavigationTarget.HomeScreen).To(new LoginViewModel(ExistingWallet), NavigationMode.Clear);
			});
		}

		public string WalletName { get; }

		public WalletManager WalletManager { get; }

		public ObservableCollection<WalletViewModelBase> Wallets { get; }

		public WalletViewModelBase? ExistingWallet { get; set; }

		public HardwareWalletOperations HardwareWalletOperations { get; }

		public ICommand OpenBrowserCommand { get; }

		public ICommand NavigateToExistingWalletLoginCommand { get; }

		private void RunDetection()
		{
			IsSearching = true;
			ExistingWalletFound = false;
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

			if (WalletManager.WalletExists(device.Fingerprint))
			{
				ExistingWallet = Wallets.FirstOrDefault(x => x.Wallet.KeyManager.MasterFingerprint == device.Fingerprint);
				Message = "The connected hardware wallet is already added to the software, click below to login or click Continue to search again.";
				ExistingWalletFound = true;
				return;
			}

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
