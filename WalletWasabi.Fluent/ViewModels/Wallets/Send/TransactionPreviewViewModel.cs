using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Security;
using System.Threading.Tasks;
using NBitcoin;
using WalletWasabi.Blockchain.TransactionBuilding;
using WalletWasabi.CoinJoin.Client.Clients.Queuing;
using WalletWasabi.Exceptions;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Gui.Helpers;
using WalletWasabi.Gui.Models.StatusBarStatuses;
using WalletWasabi.Gui.ViewModels;
using WalletWasabi.Logging;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Send
{
	[NavigationMetaData(Title = "Transaction Preview")]
	public partial class TransactionPreviewViewModel : RoutableViewModel
	{
		private readonly WalletViewModel _owner;

		public TransactionPreviewViewModel(WalletViewModel walletViewModel)
		{
			_owner = walletViewModel;
		}

		/*private async Task BuildTransactionPart2Async()
		{
			BuildTransactionResult result = await Task.Run(() => _owner.Wallet.BuildTransaction(Password, payments, feeStrategy, allowUnconfirmed: true, allowedInputs: allowedInputs, GetPayjoinClient()));

			MainWindowViewModel.Instance.StatusBar.TryAddStatus(StatusType.SigningTransaction);
			SmartTransaction signedTransaction = result.Transaction;

			if (Wallet.KeyManager.IsHardwareWallet && !result.Signed) // If hardware but still has a privkey then it's password, then meh.
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
						signedPsbt = await client.SignTxAsync(Wallet.KeyManager.MasterFingerprint.Value, result.Psbt, cts.Token);
					}
					catch (HwiException ex) when (ex.ErrorCode is not HwiErrorCode.ActionCanceled)
					{
						await PinPadViewModel.UnlockAsync();
						signedPsbt = await client.SignTxAsync(Wallet.KeyManager.MasterFingerprint.Value, result.Psbt, cts.Token);
					}
					signedTransaction = signedPsbt.ExtractSmartTransaction(result.Transaction);
				}
				finally
				{
					MainWindowViewModel.Instance.StatusBar.TryRemoveStatus(StatusType.AcquiringSignatureFromHardwareWallet);
					IsHardwareBusy = false;
				}
			}

			MainWindowViewModel.Instance.StatusBar.TryAddStatus(StatusType.BroadcastingTransaction);
			await Task.Run(async () => await Global.TransactionBroadcaster.SendTransactionAsync(signedTransaction));
		}*/

		private async Task BuildTransactionAsync(TransactionInfo info)
		{
			IsBusy = true;

			var wallet = _owner.Wallet;

			var label = info.Labels;

			var address = info.Address;

			var requests = new List<DestinationRequest>();

			var moneyRequest = MoneyRequest.Create(info.Amount, subtractFee: false);

			var feeStrategy = FeeStrategy.CreateFromFeeRate(info.FeeRate);

			var activeDestinationRequest = new DestinationRequest(address, moneyRequest, label);
			requests.Add(activeDestinationRequest);

			var intent = new PaymentIntent(requests);
			try
			{
				// todo status notification ... dequeing coins.
				var toDequeue = info.Coins.Where(x => x.CoinJoinInProgress).Select(x => x.OutPoint).ToArray();

				if (toDequeue.Any())
				{
					await wallet.ChaumianClient.DequeueCoinsFromMixAsync(toDequeue, DequeueReason.TransactionBuilding);
				}
			}
			catch
			{
				NotificationHelpers.Error("Cannot spend mixing coins.", "");
				return;
			}
			finally
			{
				// todo status notification ... dequeing coins done.
			}

			if (!wallet.KeyManager.IsWatchOnly)
			{
				try
				{
					//PasswordHelper.GetMasterExtKey(wallet.KeyManager, Password,
					//out string compatiblityPasswordUsed); // We could use TryPassword but we need the exception.
					//if (compatiblityPasswordUsed is { })
					//{
						//Password = compatiblityPasswordUsed; // Overwrite the password for BuildTransaction function.
					//	NotificationHelpers.Warning(PasswordHelper.CompatibilityPasswordWarnMessage);
					//}
				}
				catch (SecurityException ex)
				{
					NotificationHelpers.Error(ex.Message, "");
					return;
				}
				catch (Exception ex)
				{
					Logger.LogError(ex);
					NotificationHelpers.Error(ex.ToUserFriendlyString());
					return;
				}
			}

			try
			{
				//await BuildTransaction(Password, intent, feeStrategy, allowUnconfirmed: true, allowedInputs: selectedCoinReferences);
			}
			catch (InsufficientBalanceException ex)
			{
				Money needed = ex.Minimum - ex.Actual;
				NotificationHelpers.Error(
					$"Not enough coins selected. You need an estimated {needed.ToString(false, true)} BTC more to make this transaction.",
					"");
			}
			catch (HttpRequestException ex)
			{
				NotificationHelpers.Error(ex.ToUserFriendlyString());
				Logger.LogError(ex);
			}
			catch (Exception ex)
			{
				NotificationHelpers.Error(ex.ToUserFriendlyString(), sender: _owner.Wallet);
				Logger.LogError(ex);
			}
			finally
			{
				MainWindowViewModel.Instance.StatusBar.TryRemoveStatus(StatusType.BuildingTransaction,
					StatusType.SigningTransaction, StatusType.BroadcastingTransaction);
				IsBusy = false;
			}
		}
	}
}