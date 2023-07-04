using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Extensions;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.Infrastructure;
using WalletWasabi.Fluent.Models.UI;
using WalletWasabi.Fluent.ViewModels.Dialogs;
using WalletWasabi.Models;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Home.History.HistoryItems;

public class TransactionHistoryItemViewModel : HistoryItemViewModelBase
{
	public TransactionHistoryItemViewModel(
		UiContext uiContext,
		int orderIndex,
		TransactionSummary transactionSummary,
		WalletViewModel walletVm,
		Money balance)
		: base(orderIndex, transactionSummary)
	{
		Labels = transactionSummary.Labels;
		IsConfirmed = transactionSummary.IsConfirmed();
		Date = transactionSummary.DateTime.ToLocalTime();
		Balance = balance;

		var confirmations = transactionSummary.GetConfirmations();
		ConfirmedToolTip = $"{confirmations} confirmation{TextHelpers.AddSIfPlural(confirmations)}";

		SetAmount(transactionSummary.Amount, transactionSummary.Fee);

		ShowDetailsCommand = ReactiveCommand.Create(() => UiContext.Navigate().To().TransactionDetails(transactionSummary, walletVm));

		CanSpeedUpTransaction = transactionSummary.Transaction.IsSpeedupable;

		var keyManager = walletVm.Wallet.KeyManager;

		CanCancelTransaction = transactionSummary.Transaction.IsCancelable(keyManager);

		SpeedUpTransactionCommand = ReactiveCommand.Create(
			() =>
			{
				var tx = transactionSummary.Transaction;
				var change = tx.GetWalletOutputs(keyManager).FirstOrDefault();
				var isDestinationAmountModified = false;
				var txSizeBytes = tx.Transaction.GetVirtualSize();
				var bestFeeRate = walletVm.Wallet.FeeProvider.AllFeeEstimate?.GetFeeRate(2);
				if (bestFeeRate is null)
				{
					throw new NullReferenceException("bestFeeRate is null. This should never happen.");
				}

				if (tx.GetForeignInputs(keyManager).Any() || !tx.IsRBF)
				{
					// IF there are any foreign input or doesn't signal RBF, then we can only CPFP.
					if (change is null)
					{
						// IF change is not present, we cannot do anything with it.
						throw new InvalidOperationException("Transaction doesn't signal RBF, nor we have change to CPFP it.");
					}

					// Let's build a CPFP with best fee rate temporarily.
					var tempTx = TransactionHelpers.BuildChangelessTransaction(
						walletVm.Wallet,
						keyManager.GetNextChangeKey().GetAssumedScriptPubKey().GetDestinationAddress(walletVm.Wallet.Network) ?? throw new NullReferenceException($"GetDestinationAddress returned null. This should never happen."),
						LabelsArray.Empty,
						bestFeeRate,
						tx.GetWalletInputs(keyManager),
						tryToSign: true);
					var tempTxSizeBytes = tempTx.Transaction.Transaction.GetVirtualSize();

					// Let's increase the fee rate of CPFP transaction.
					var cpfpFee = (long)((txSizeBytes + tempTxSizeBytes) * bestFeeRate.SatoshiPerByte) + 1;
					var cpfpFeeRate = new FeeRate((decimal)(cpfpFee / tempTxSizeBytes));

					var cpfp = TransactionHelpers.BuildChangelessTransaction(
						walletVm.Wallet,
						keyManager.GetNextChangeKey().GetAssumedScriptPubKey().GetDestinationAddress(walletVm.Wallet.Network) ?? throw new NullReferenceException($"GetDestinationAddress returned null. This should never happen."),
						LabelsArray.Empty,
						cpfpFeeRate,
						tx.GetWalletInputs(keyManager),
						tryToSign: true);
				}
				else
				{
					// Else it's RBF.
					var originalFeeRate = tx.Transaction.GetFeeRate(tx.GetWalletInputs(keyManager).Select(x => x.Coin).Cast<ICoin>().ToArray());

					// If the highest fee rate is smaller or equal than the original fee rate, then increase fee rate minimally, otherwise built tx with best fee rate.
					FeeRate rbfFeeRate = bestFeeRate is null || bestFeeRate <= originalFeeRate
						? new FeeRate(originalFeeRate.SatoshiPerByte + Money.Satoshis(Math.Max(2, originalFeeRate.SatoshiPerByte * 0.05m)).Satoshi)
						: bestFeeRate;

					var originalTransaction = transactionSummary.Transaction.Transaction;
					var rbfTransaction = originalTransaction.Clone();
					rbfTransaction.Outputs.Clear();

					if (!tx.GetForeignOutputs(keyManager).Any())
					{
						// IF self spend.
					}
					else
					{
						// IF send.
						if (change is not null)
						{
							// IF change present, then we modify the change's amount.
						}
						else
						{
							// IF change not present, then we modify the destination's amount.
							isDestinationAmountModified = true;
						}
					}
				}

				uiContext.Navigate().To().BoostTransactionDialog(new BoostedTransactionPreview(walletVm.Wallet.Synchronizer.UsdExchangeRate)
				{
					Destination = "some destination",
					Amount = Money.FromUnit(1234, MoneyUnit.Satoshi),
					Labels = new LabelsArray("label1", "label2", "label3"),
					Fee = Money.FromUnit(25, MoneyUnit.Satoshi),
					ConfirmationTime = TimeSpan.FromMinutes(20),
				});
			});

		CancelTransactionCommand = ReactiveCommand.Create(
			() =>
			{
				var tx = transactionSummary.Transaction;
				var change = tx.GetWalletOutputs(keyManager).FirstOrDefault();
				var originalFeeRate = tx.Transaction.GetFeeRate(tx.GetWalletInputs(keyManager).Select(x => x.Coin).Cast<ICoin>().ToArray());
				var cancelFeeRate = new FeeRate(originalFeeRate.SatoshiPerByte + Money.Satoshis(2).Satoshi);
				var originalTransaction = transactionSummary.Transaction.Transaction;
				var cancelTransaction = originalTransaction.Clone();
				cancelTransaction.Outputs.Clear();

				if (change is not null)
				{
					// IF change present THEN make the change the only output
					// Add a dummy output to make the transaction size proper.
					cancelTransaction.Outputs.Add(Money.Zero, change.TxOut.ScriptPubKey);
					var cancelFee = (long)(originalTransaction.GetVirtualSize() * cancelFeeRate.SatoshiPerByte) + 1;
					cancelTransaction.Outputs.Clear();
					cancelTransaction.Outputs.Add(tx.GetWalletInputs(keyManager).Sum(x => x.Amount.Satoshi) - cancelFee, change.TxOut.ScriptPubKey);
				}
				else
				{
					// ELSE THEN replace the output with a new output that's ours
					// Add a dummy output to make the transaction size proper.
					var newOwnOutput = keyManager.GetNextChangeKey();
					cancelTransaction.Outputs.Add(Money.Zero, newOwnOutput.GetAssumedScriptPubKey());
					var cancelFee = (long)(originalTransaction.GetVirtualSize() * cancelFeeRate.SatoshiPerByte) + 1;
					cancelTransaction.Outputs.Clear();
					cancelTransaction.Outputs.Add(tx.GetWalletInputs(keyManager).Sum(x => x.Amount.Satoshi) - cancelFee, newOwnOutput.GetAssumedScriptPubKey());
				}

				// Signing
				TransactionBuilder builder = walletVm.Wallet.Network.CreateTransactionBuilder();

				var secrets = tx.WalletInputs
					.SelectMany(coin => walletVm.Wallet.KeyManager.GetSecrets(walletVm.Wallet.Kitchen.SaltSoup(), coin.ScriptPubKey))
					.ToArray();

				builder.AddKeys(secrets);
				builder.AddCoins(tx.WalletInputs.Select(x => x.Coin));

				var coins = tx.WalletInputs.Select(x => (ICoin)x.Coin);
				var keys = secrets.Select(key => key.GetBitcoinSecret(walletVm.Wallet.Network, 000));
				cancelTransaction.Sign(keys, coins);

				var signedCancelSmartTransaction = new SmartTransaction(
					cancelTransaction,
					Height.Mempool,
					isReplacement: true);

				foreach (var input in tx.WalletInputs)
				{
					signedCancelSmartTransaction.TryAddWalletInput(input);
				}

				uiContext.Navigate().To().CancelTransactionDialog(walletVm.Wallet, transactionSummary.Transaction, signedCancelSmartTransaction);
			},
			Observable.Return(CanCancelTransaction));

		DateString = Date.ToLocalTime().ToUserFacingString();
	}

	public bool CanCancelTransaction { get; }

	public bool CanSpeedUpTransaction { get; }
}
