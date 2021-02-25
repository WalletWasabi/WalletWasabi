using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NBitcoin;
using ReactiveUI;
using Splat;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Blockchain.TransactionBuilding;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.CoinJoin.Client.Clients.Queuing;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.ViewModels.Dialogs;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Gui;
using WalletWasabi.Gui.Controls.WalletExplorer;
using WalletWasabi.Gui.Models.StatusBarStatuses;
using WalletWasabi.Gui.ViewModels;
using WalletWasabi.Hwi;
using WalletWasabi.Hwi.Exceptions;
using WalletWasabi.Userfacing;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Send
{
	[NavigationMetaData(Title = "Transaction Preview")]
	public partial class TransactionPreviewViewModel : RoutableViewModel
	{
		private readonly BuildTransactionResult _transaction;

		public TransactionPreviewViewModel(Wallet wallet, TransactionInfo info, BuildTransactionResult transaction)
		{
			_transaction = transaction;

			var destinationAmount = transaction.CalculateDestinationAmount().ToDecimal(MoneyUnit.BTC);

			var fee = transaction.Fee;

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

			PercentFeeText = $"{transaction.FeePercentOfSent:F2}%";

			NextCommand = ReactiveCommand.CreateFromTask(async () =>
			{
				var dialogResult =
					await NavigateDialog(new EnterPasswordDialogViewModel(""), NavigationTarget.DialogScreen);

				if (dialogResult.Kind == DialogResultKind.Normal)
				{
					IsBusy = true;

					string? compatibilityPasswordUsed;

					var passwordValid = await Task.Run(
						() => PasswordHelper.TryPassword(
							wallet.KeyManager,
							dialogResult.Result,
							out compatibilityPasswordUsed));

					IsBusy = false;

					if (passwordValid)
					{
						// Dequeue any coin-joining coins.
						await wallet.ChaumianClient.DequeueAllCoinsFromMixAsync(DequeueReason.TransactionBuilding);

						// Broadcast transaction.
						var signedTransaction = _transaction.Transaction;

						// If it's a hardware wallet and still has a private key then it's password.
						if (wallet.KeyManager.IsHardwareWallet && !_transaction.Signed)
						{
							try
							{
								var client = new HwiClient(wallet.Network);

								using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(3));
								PSBT signedPsbt = null;
								try
								{
									signedPsbt = await client.SignTxAsync(wallet.KeyManager.MasterFingerprint.Value,
										_transaction.Psbt, cts.Token);
								}
								catch (HwiException ex) when (ex.ErrorCode is not HwiErrorCode.ActionCanceled)
								{
									await PinPadViewModel.UnlockAsync();
									signedPsbt = await client.SignTxAsync(wallet.KeyManager.MasterFingerprint.Value,
										_transaction.Psbt, cts.Token);
								}

								signedTransaction = signedPsbt.ExtractSmartTransaction(_transaction.Transaction);
							}
							catch (Exception _)
							{
								// probably throw something here?
							}
						}

						await Locator.Current.GetService<Global>().TransactionBroadcaster
							.SendTransactionAsync(signedTransaction);
					}
					else
					{
						await ShowErrorAsync("Password was incorrect.", "Please try again.", "");
					}
				}
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