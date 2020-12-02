using System;
using System.Threading.Tasks;
using System.Windows.Input;
using ReactiveUI;
using WalletWasabi.Extensions;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Hwi.Models;
using WalletWasabi.Logging;

namespace WalletWasabi.Fluent.ViewModels.AddWallet.HardwareWallet
{
	public class DetectedHardwareWalletViewModel : RoutableViewModel
	{

		public DetectedHardwareWalletViewModel(HardwareWalletOperations walletOperations, string walletName)
		{
			WalletName = walletName;
			var type = WalletType.Hardware;

			switch (walletOperations.SelectedDevice!.Model)
			{
				case HardwareWalletModels.Coldcard:
				case HardwareWalletModels.Coldcard_Simulator:
					type = WalletType.Coldcard;
					break;

				case HardwareWalletModels.Ledger_Nano_S:
					type = WalletType.Ledger;
					break;

				case HardwareWalletModels.Trezor_1:
				case HardwareWalletModels.Trezor_1_Simulator:
				case HardwareWalletModels.Trezor_T:
				case HardwareWalletModels.Trezor_T_Simulator:
					type = WalletType.Trezor;
					break;
			}

			Type = type;
			TypeName = walletOperations.SelectedDevice.Model.FriendlyName();

			NextCommand = ReactiveCommand.CreateFromTask(
				async () =>
			{
				IsBusy = true;

				await Task.Run(async () => await walletOperations.GenerateWalletAsync(WalletName));

				Navigate().To(new AddedWalletPageViewModel(WalletName, Type));

				IsBusy = false;
			});

			NoCommand = ReactiveCommand.Create(
				() =>
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
