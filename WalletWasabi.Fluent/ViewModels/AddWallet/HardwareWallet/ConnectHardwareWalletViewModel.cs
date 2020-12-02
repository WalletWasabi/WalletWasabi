using System;
using System.Linq;
using System.Reactive.Disposables;
using System.Threading.Tasks;
using System.Timers;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Hwi.Exceptions;
using WalletWasabi.Hwi.Models;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.AddWallet.HardwareWallet
{
	public class ConnectHardwareWalletViewModel : RoutableViewModel
	{
		private string _message;
		private bool _nothingShowUpMessageVisibility;

		public ConnectHardwareWalletViewModel(string walletName, Network network, WalletManager walletManager)
		{
			WalletName = walletName;
			HardwareWalletOperations = new HardwareWalletOperations(walletManager, network);

			_message = "";
		}

		public bool NothingShowUpMessageVisibility
		{
			get => _nothingShowUpMessageVisibility;
			set => this.RaiseAndSetIfChanged(ref _nothingShowUpMessageVisibility, value);
		}

		public string WalletName { get; }

		public string Message
		{
			get => _message;
			set => this.RaiseAndSetIfChanged(ref _message, value);
		}

		public HardwareWalletOperations HardwareWalletOperations { get; }

		private void OnSearchingHasNoResult(object? sender, EventArgs e)
		{
			NothingShowUpMessageVisibility = true;
		}

		private void OnPassphraseNeeded(object sender, ElapsedEventArgs e)
		{
			Message = "Check your device and enter your passphrase.";
		}

		private void OnHardwareWalletsFound(object? sender, HwiEnumerateEntry[] devices)
		{
			NothingShowUpMessageVisibility = false;

			if (devices.Length > 1)
			{
				Message = "Make sure you have only one hardware wallet connected to the PC.";
				return;
			}

			var device = devices[0];

			if (device.Code is { })
			{
				Message = "Something happened with your device, please reconnect it to the PC.";
				return;
			}

			if (device.NeedsPassphraseSent == true)
			{
				Message = "Enter your passphrase on your device.";
			}

			if (device.NeedsPinSent == true)
			{
				Message = "Enter your PIN on your device.";
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

			HardwareWalletOperations.SelectedDevice = device;
			Navigate().To(new DetectedHardwareWalletViewModel(HardwareWalletOperations, WalletName));
		}

		protected override void OnNavigatedTo(bool inStack, CompositeDisposable disposable)
		{
			base.OnNavigatedTo(inStack, disposable);

			HardwareWalletOperations.HardwareWalletsFound += OnHardwareWalletsFound;
			HardwareWalletOperations.SearchingHasNoResult += OnSearchingHasNoResult;
			HardwareWalletOperations.PassphraseTimer.Elapsed += OnPassphraseNeeded;

			disposable.Add(Disposable.Create(() =>
			{
				HardwareWalletOperations.HardwareWalletsFound -= OnHardwareWalletsFound;
				HardwareWalletOperations.SearchingHasNoResult -= OnSearchingHasNoResult;
				HardwareWalletOperations.PassphraseTimer.Elapsed -= OnPassphraseNeeded;
			}));
		}
	}
}
