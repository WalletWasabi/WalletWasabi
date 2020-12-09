using System;
using System.Windows.Input;
using ReactiveUI;
using WalletWasabi.Extensions;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Hwi.Models;

namespace WalletWasabi.Fluent.ViewModels.AddWallet.HardwareWallet
{
	public class DetectedHardwareWalletViewModel : RoutableViewModel
	{
		public DetectedHardwareWalletViewModel(HardwareWalletOperations hardwareWalletOperations, string walletName)
		{
			WalletName = walletName;

			Title = "Hardware Wallet";
			ErrorCaption = "Error occured during adding your wallet.";

			switch (hardwareWalletOperations.SelectedDevice!.Model)
			{
				case HardwareWalletModels.Coldcard:
				case HardwareWalletModels.Coldcard_Simulator:
					Type = WalletType.Coldcard;
					break;

				case HardwareWalletModels.Ledger_Nano_S:
					Type = WalletType.Ledger;
					break;

				case HardwareWalletModels.Trezor_1:
				case HardwareWalletModels.Trezor_1_Simulator:
				case HardwareWalletModels.Trezor_T:
				case HardwareWalletModels.Trezor_T_Simulator:
					Type = WalletType.Trezor;
					break;
				default:
					Type = WalletType.Hardware;
					break;
			}

			TypeName = hardwareWalletOperations.SelectedDevice.Model.FriendlyName();

			NextCommand = ReactiveCommand.CreateFromTask(async () =>
			{
				IsBusy = true;

				try
				{
					await hardwareWalletOperations.GenerateWalletAsync(WalletName);
					hardwareWalletOperations.Dispose();
					Navigate().To(new AddedWalletPageViewModel(WalletName, Type));
				}
				catch(Exception ex)
				{
					ShowError(ex.Message);
					Navigate().Back();
				}

				IsBusy = false;
			});

			NoCommand = ReactiveCommand.Create(() =>
			{
				Navigate().Back();
			});
		}

		public string WalletName { get; }

		public WalletType Type { get; }

		public string TypeName { get; }

		public ICommand NoCommand { get; }
	}
}
