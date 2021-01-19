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
	public class DetectedHardwareWalletViewModel : RoutableViewModel
	{
		public DetectedHardwareWalletViewModel(WalletManager walletManager, string walletName, HwiEnumerateEntry device)
		{
			Title = "Hardware Wallet";
			WalletName = walletName;

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
					using var hwo = new HardwareWalletOperations(walletManager);

					var km = await hwo.GenerateWalletAsync(WalletName, device);
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
