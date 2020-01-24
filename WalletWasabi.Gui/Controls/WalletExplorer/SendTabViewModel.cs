using NBitcoin;
using System;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Blockchain.TransactionBuilding;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Gui.Helpers;
using WalletWasabi.Gui.Models.StatusBarStatuses;
using WalletWasabi.Gui.ViewModels;
using WalletWasabi.Hwi;
using WalletWasabi.Hwi.Exceptions;

namespace WalletWasabi.Gui.Controls.WalletExplorer
{
	public class SendTabViewModel : SendControlViewModel
	{
		public override string DoButtonText => "Send Transaction";
		public override string DoingButtonText => "Sending Transaction...";

		public SendTabViewModel(WalletViewModel walletViewModel) : base(walletViewModel, "Send")
		{
		}

		protected override async Task DoAfterBuildTransaction(BuildTransactionResult result)
		{
			MainWindowViewModel.Instance.StatusBar.TryAddStatus(StatusType.SigningTransaction);
			SmartTransaction signedTransaction = result.Transaction;

			if (IsHardwareWallet && !result.Signed) // If hardware but still has a privkey then it's password, then meh.
			{
				try
				{
					IsHardwareBusy = true;
					MainWindowViewModel.Instance.StatusBar.TryAddStatus(StatusType.AcquiringSignatureFromHardwareWallet);
					var client = new HwiClient(Global.Network);

					using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(3));
					PSBT signedPsbt = null;
					try
					{
						signedPsbt = await client.SignTxAsync(KeyManager.MasterFingerprint.Value, result.Psbt, cts.Token);
					}
					catch (HwiException)
					{
						await PinPadViewModel.UnlockAsync();
						signedPsbt = await client.SignTxAsync(KeyManager.MasterFingerprint.Value, result.Psbt, cts.Token);
					}
					signedTransaction = signedPsbt.ExtractSmartTransaction(result.Transaction);
				}
				catch (Exception ex)
				{
					NotificationHelpers.Error(ex.ToUserFriendlyString());
					return;
				}
				finally
				{
					MainWindowViewModel.Instance.StatusBar.TryRemoveStatus(StatusType.AcquiringSignatureFromHardwareWallet);
					IsHardwareBusy = false;
				}
			}

			MainWindowViewModel.Instance.StatusBar.TryAddStatus(StatusType.BroadcastingTransaction);
			await Task.Run(async () => await Global.TransactionBroadcaster.SendTransactionAsync(signedTransaction));

			ResetUi();
		}
	}
}
