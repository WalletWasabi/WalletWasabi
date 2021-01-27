using System;
using System.Windows.Input;
using ReactiveUI;
using WalletWasabi.Extensions;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Hwi.Models;
using WalletWasabi.Logging;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.AddWallet.HardwareWallet
{
	[NavigationMetaData(Title = "Hardware Wallet")]
	public partial class DetectedHardwareWalletViewModel : RoutableViewModel
	{
		public DetectedHardwareWalletViewModel(HardwareWalletOperations hardwareWalletOperations, string walletName)
		{
			WalletName = walletName;

			Type = hardwareWalletOperations.SelectedDevice!.Model switch
			{
				HardwareWalletModels.Coldcard or HardwareWalletModels.Coldcard_Simulator => WalletType.Coldcard,
				HardwareWalletModels.Ledger_Nano_S => WalletType.Ledger,
				HardwareWalletModels.Trezor_1 or HardwareWalletModels.Trezor_1_Simulator or HardwareWalletModels.Trezor_T or HardwareWalletModels.Trezor_T_Simulator => WalletType.Trezor,
				_ => WalletType.Hardware,
			};

			TypeName = hardwareWalletOperations.SelectedDevice.Model.FriendlyName();

			NextCommand = ReactiveCommand.CreateFromTask(async () =>
			{
				try
				{
					var km = await hardwareWalletOperations.GenerateWalletAsync(WalletName);
					var walletManager = hardwareWalletOperations.WalletManager;
					hardwareWalletOperations.Dispose();
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

		public string WalletName { get; }

		public WalletType Type { get; }

		public string TypeName { get; }

		public ICommand NoCommand { get; }
	}
}
