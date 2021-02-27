using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NBitcoin;
using ReactiveUI;
using SharpDX;
using WalletWasabi.Blockchain.TransactionBroadcasting;
using WalletWasabi.Blockchain.TransactionBuilding;
using WalletWasabi.CoinJoin.Client.Clients.Queuing;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.ViewModels.Dialogs;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Gui.Controls.WalletExplorer;
using WalletWasabi.Hwi;
using WalletWasabi.Hwi.Exceptions;
using WalletWasabi.Userfacing;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Send
{
	[NavigationMetaData(Title = "Transaction Preview")]
	public partial class TransactionPreviewViewModel : RoutableViewModel
	{
		public TransactionPreviewViewModel(Wallet wallet, TransactionInfo info, TransactionBroadcaster broadcaster,
			BuildTransactionResult buildTransactionResult)
		{
			var destinationAmount = buildTransactionResult.CalculateDestinationAmount().ToDecimal(MoneyUnit.BTC);

			var fee = buildTransactionResult.Fee;

			var labels = "";

			if (info.Labels.Count() == 1)
			{
				labels = info.Labels.First() + " ";
			}
			else if (info.Labels.Count() > 1)
			{
				labels = string.Join(", ", info.Labels.Take(info.Labels.Count() - 1));

				labels += $" and {info.Labels.Last()} ";
			}

			BtcAmountText = $"{destinationAmount} bitcoins ";

			FiatAmountText = $"(≈{(destinationAmount * wallet.Synchronizer.UsdExchangeRate).FormattedFiat()} USD) ";

			LabelsText = labels;

			AddressText = info.Address.ToString();

			ConfirmationTimeText = "~20 minutes ";

			BtcFeeText = $"{fee.ToDecimal(MoneyUnit.Satoshi)} satoshis ";

			FiatFeeText =
				$"(≈{(fee.ToDecimal(MoneyUnit.BTC) * wallet.Synchronizer.UsdExchangeRate).FormattedFiat()} USD)";

			PercentFeeText = $"{buildTransactionResult.FeePercentOfSent:F2}%";

			NextCommand = ReactiveCommand.CreateFromTask(async () =>
			{
				var transaction = buildTransactionResult;

				var authDialog = AuthorisationHelpers.GetAuthorisationDialog(wallet, ref transaction);
				var authResult = await NavigateDialog(authDialog, NavigationTarget.DialogScreen);

				if (authResult.Result)
				{
					await broadcaster.SendTransactionAsync(transaction.Transaction);
					Navigate().Clear();
				}
				// else
				// {
				// 	await ShowErrorAsync(authErrorMessage, "Please try again.", "");
				// }

				// IsBusy = true;
				//
				// var passwordValid = await Task.Run(
				// 	() => PasswordHelper.TryPassword(
				// 		wallet.KeyManager,
				// 		dialogResult.Result,
				// 		out string? compatibilityPasswordUsed));
				//
				// if (passwordValid)
				// {
				// 	// Dequeue any coin-joining coins.
				// 	await wallet.ChaumianClient.DequeueAllCoinsFromMixAsync(DequeueReason.TransactionBuilding);
				//
				// 	var signedTransaction2 = buildTransactionResult.Transaction;
				//
				// 	// If it's a hardware wallet and still has a private key then it's password.
				// 	if (wallet.KeyManager.IsHardwareWallet && !buildTransactionResult.Signed)
				// 	{
				// 		try
				// 		{
				// 			var client = new HwiClient(wallet.Network);
				//
				// 			using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(3));
				// 			PSBT? signedPsbt = null;
				// 			try
				// 			{
				// 				signedPsbt = await client.SignTxAsync(
				// 					wallet.KeyManager.MasterFingerprint!.Value,
				// 					buildTransactionResult.Psbt,
				// 					cts.Token);
				// 			}
				// 			catch (HwiException ex) when (ex.ErrorCode is not HwiErrorCode.ActionCanceled)
				// 			{
				// 				await PinPadViewModel.UnlockAsync();
				//
				// 				signedPsbt = await client.SignTxAsync(
				// 					wallet.KeyManager.MasterFingerprint!.Value,
				// 					buildTransactionResult.Psbt,
				// 					cts.Token);
				// 			}
				//
				// 			signedTransaction2 = signedPsbt.ExtractSmartTransaction(buildTransactionResult.Transaction);
				// 		}
				// 		catch (Exception _)
				// 		{
				// 			// probably throw something here?
				// 		}
				// 	}
				//
				// 	await broadcaster.SendTransactionAsync(signedTransaction);
				//
				// 	Navigate().Clear();
				//
				// 	IsBusy = false;
					// }
			});
		}

		public string BtcAmountText { get; }

		public string FiatAmountText { get; }

		public string LabelsText { get; }

		public string AddressText { get; }

		public string ConfirmationTimeText { get; }

		public string BtcFeeText { get; }

		public string FiatFeeText { get; }

		public string PercentFeeText { get; }
	}
}
