using System.Collections.Generic;
using System.Linq;
using NBitcoin;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Extensions;
using WalletWasabi.Models;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.Helpers;

public static class TransactionCancellationHelper
{
	public static SmartTransaction CreateCancellation(SmartTransaction transactionToCancel, Wallet wallet)
	{
		var keyManager = wallet.KeyManager;
		var network = wallet.Network;

		if (transactionToCancel.GetWalletInputs(keyManager).Count() != transactionToCancel.Transaction.Inputs.Count)
		{
			throw new InvalidOperationException("Transaction has foreign inputs. Cannot cancel.");
		}

		// Take the first own output and if we have it that's where we send all our money to.
		var ownOutput = transactionToCancel.GetWalletOutputs(keyManager).FirstOrDefault();

		// Calculate the original fee rate and fee.
		var originalFeeRate = transactionToCancel.Transaction.GetFeeRate(transactionToCancel.GetWalletInputs(keyManager).Select(x => x.Coin).Cast<ICoin>().ToArray());
		var originalFee = transactionToCancel.Transaction.GetFee(transactionToCancel.WalletInputs.Select(x => x.Coin).ToArray());

		SmartTransaction cancelTransaction;
		int i = 1;
		do
		{
			cancelTransaction = TransactionHelpers.BuildChangelessTransaction(
				wallet,
				ownOutput?.Coin.ScriptPubKey.GetDestinationAddress(network) ?? keyManager.GetNextChangeKey().GetAssumedScriptPubKey().GetDestinationAddress(network) ?? throw new NullReferenceException("GetDestinationAddress returned null. This should never happen."),
				LabelsArray.Empty,
				new FeeRate(originalFeeRate.SatoshiPerByte + i),
				transactionToCancel.WalletInputs,
				allowDoubleSpend: true,
				tryToSign: true)
			.Transaction;

			// Double i, so we should be able to find a suitable cancel tx in a few iterations.
			i *= 2;
		} while (originalFee >= cancelTransaction.Transaction.GetFee(cancelTransaction.WalletInputs.Select(x => x.Coin).ToArray()));

		return cancelTransaction;
	}
}
