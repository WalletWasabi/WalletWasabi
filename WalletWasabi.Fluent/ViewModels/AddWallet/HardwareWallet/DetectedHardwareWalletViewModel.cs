using System;
using System.Reactive.Disposables;
using System.Windows.Input;
using ReactiveUI;
using WalletWasabi.Extensions;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Hwi.Models;
using WalletWasabi.Logging;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.AddWallet.HardwareWallet
{
	public class DetectedHardwareWalletViewModel : RoutableViewModel
	{
		public DetectedHardwareWalletViewModel(WalletManager walletManager, string walletName, HwiEnumerateEntry device)
		{
			Title = "Hardware Wallet";
			WalletManager = walletManager;
			WalletName = walletName;
			HardwareWalletOperations = new HardwareWalletOperations(walletManager.Network);

			Type = device.Model switch
			{
				HardwareWalletModels.Coldcard or HardwareWalletModels.Coldcard_Simulator => WalletType.Coldcard,
				HardwareWalletModels.Ledger_Nano_S => WalletType.Ledger,
				HardwareWalletModels.Trezor_1 or HardwareWalletModels.Trezor_1_Simulator or HardwareWalletModels.Trezor_T or HardwareWalletModels.Trezor_T_Simulator => WalletType.Trezor,
				_ => WalletType.Hardware,
			};

			TypeName = device.Model.FriendlyName();

			NextCommand = ReactiveCommand.CreateFromTask(async () =>
			{
				try
				{
					var walletFilePath = WalletManager.WalletDirectories.GetWalletFilePaths(WalletName).walletFilePath;

					var km = await HardwareWalletOperations.GenerateWalletAsync(device, walletFilePath);
					km.SetIcon(Type);

					Navigate().To(new AddedWalletPageViewModel(walletManager, km));
				}
				catch (Exception ex)
				{
					Logger.LogError(ex);
					await ShowErrorAsync(ex.ToUserFriendlyString(), "Error occured during adding your wallet.");
					Navigate().Back();
				}
			});

			NoCommand = ReactiveCommand.Create(() => Navigate().Back());

			EnableAutoBusyOn(NextCommand);
		}

		public WalletManager WalletManager { get; }
		public string WalletName { get; }

		public WalletType Type { get; }

		public string TypeName { get; }

		public ICommand NoCommand { get; }

		private HardwareWalletOperations HardwareWalletOperations { get; set; }

		protected override void OnNavigatedTo(bool inStack, CompositeDisposable disposable)
		{
			base.OnNavigatedTo(inStack, disposable);

			Disposable.Create(async () => await HardwareWalletOperations.DisposeAsync()).DisposeWith(disposable);
		}
	}
}
