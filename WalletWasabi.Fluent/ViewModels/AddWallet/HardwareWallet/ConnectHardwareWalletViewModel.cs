using System;
using System.Linq;
using System.Reactive.Disposables;
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

			if (!device.IsInitialized())
			{
				Message = "Check your device and finish the initialization.";
				// TODO: execute init if possible
				return;
			}

			if (device.Code is { } errorCode)
			{
				ShowErrorMessage(errorCode);
				return;
			}

			HardwareWalletOperations.SelectedDevice = device;
			Navigate().To(new DetectedHardwareWalletViewModel(HardwareWalletOperations, WalletName));
		}

		private void ShowErrorMessage(HwiErrorCode errorCode)
		{
			// TODO: Show message that helps the user

			switch (errorCode)
			{
				case HwiErrorCode.NoDeviceType:
					break;
				case HwiErrorCode.MissingArguments:
					break;
				case HwiErrorCode.DeviceConnError:
					break;
				case HwiErrorCode.UnknownDeviceType:
					break;
				case HwiErrorCode.InvalidTx:
					break;
				case HwiErrorCode.NoPassword:
					break;
				case HwiErrorCode.BadArgument:
					break;
				case HwiErrorCode.NotImplemented:
					break;
				case HwiErrorCode.UnavailableAction:
					break;
				case HwiErrorCode.DeviceAlreadyInit:
					break;
				case HwiErrorCode.DeviceAlreadyUnlocked:
					break;
				case HwiErrorCode.DeviceNotReady:
					break;
				case HwiErrorCode.UnknownError:
					break;
				case HwiErrorCode.ActionCanceled:
					break;
				case HwiErrorCode.DeviceBusy:
					break;
				case HwiErrorCode.NeedToBeRoot:
					break;
				case HwiErrorCode.HelpText:
					break;
				case HwiErrorCode.DeviceNotInitialized:
					break;
				default:
					throw new ArgumentOutOfRangeException(nameof(errorCode), errorCode, null);
			}
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
