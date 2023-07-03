using System.Linq;
using System.Reactive.Linq;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Blockchain.Transactions;
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
					var cancelFee = (long)(cancelTransaction.GetVirtualSize() * cancelFeeRate.SatoshiPerByte) + 1;
					cancelTransaction.Outputs.Clear();
					cancelTransaction.Outputs.Add(tx.GetWalletInputs(keyManager).Sum(x => x.Amount.Satoshi) - cancelFee, change.TxOut.ScriptPubKey);
				}
				else
				{
					// ELSE THEN replace the output with a new output that's ours
					// Add a dummy output to make the transaction size proper.
					var newOwnOutput = keyManager.GetNextChangeKey();
					cancelTransaction.Outputs.Add(Money.Zero, newOwnOutput.GetAssumedScriptPubKey());
					var cancelFee = (long)(cancelTransaction.GetVirtualSize() * cancelFeeRate.SatoshiPerByte) + 1;
					cancelTransaction.Outputs.Clear();
					cancelTransaction.Outputs.Add(tx.GetWalletInputs(keyManager).Sum(x => x.Amount.Satoshi) - cancelFee, newOwnOutput.GetAssumedScriptPubKey());
				}

				var cancelSmartTransaction = new SmartTransaction(
					cancelTransaction,
					Height.Mempool,
					isReplacement: true);

				foreach (var input in tx.WalletInputs)
				{
					cancelSmartTransaction.TryAddWalletInput(input);
				}

				uiContext.Navigate().To().CancelTransactionDialog(transactionSummary.Transaction, cancelSmartTransaction);
			},
			Observable.Return(CanCancelTransaction));

		DateString = Date.ToLocalTime().ToUserFacingString();
	}

	public bool CanCancelTransaction { get; }

	public bool CanSpeedUpTransaction { get; }
}
