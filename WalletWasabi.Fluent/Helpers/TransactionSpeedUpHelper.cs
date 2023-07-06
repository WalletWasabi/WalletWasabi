using System.Linq;
using NBitcoin;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Models;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.Helpers;

internal static class TransactionSpeedUpHelper
{
	public static SmartTransaction CreateSpeedUpTransaction(SmartTransaction transactionToSpeedUp, Wallet wallet)
	{
		var keyManager = wallet.KeyManager;
		var network = wallet.Network;

		if (transactionToSpeedUp.GetWalletOutputs(keyManager).Count() == transactionToSpeedUp.Transaction.Outputs.Count)
		{
			throw new InvalidOperationException("Transaction has no foreign outputs. Cannot speed up.");
		}

		// Take the largest own output and if we have it that's what we will want to CPFP or deduct RBF fee from.
		var ownOutput = transactionToSpeedUp.GetWalletOutputs(keyManager).OrderByDescending(x => x.Amount).FirstOrDefault();
		var txSizeBytes = transactionToSpeedUp.Transaction.GetVirtualSize();

		bool isDestinationAmountModified = false;
		bool isRBF = false;
		SmartTransaction newTransaction;

		var bestFeeRate = wallet.FeeProvider.AllFeeEstimate?.GetFeeRate(2);
		if (bestFeeRate is null)
		{
			throw new NullReferenceException($"{nameof(bestFeeRate)} is null. This should never happen.");
		}

		if (transactionToSpeedUp.GetForeignInputs(keyManager).Any() || !transactionToSpeedUp.IsRBF)
		{
			// IF there are any foreign input or doesn't signal RBF, then we can only CPFP.
			isRBF = false;

			if (ownOutput is null)
			{
				// IF change is not present, we cannot do anything with it.
				throw new InvalidOperationException("Transaction doesn't signal RBF, nor we have change to CPFP it.");
			}

			// Let's build a CPFP with best fee rate temporarily.
			var tempTx = TransactionHelpers.BuildChangelessTransaction(
				wallet,
				keyManager.GetNextChangeKey().GetAssumedScriptPubKey().GetDestinationAddress(network) ?? throw new NullReferenceException("GetDestinationAddress returned null. This should never happen."),
				LabelsArray.Empty,
				bestFeeRate,
				new[] { ownOutput },
				tryToSign: true);
			var tempTxSizeBytes = tempTx.Transaction.Transaction.GetVirtualSize();

			// Let's increase the fee rate of CPFP transaction.
			// Let's assume the transaction we want to CPFP pays 0 fees.
			var cpfpFee = (long)((txSizeBytes + tempTxSizeBytes) * bestFeeRate.SatoshiPerByte) + 1;
			var cpfpFeeRate = new FeeRate((decimal)(cpfpFee / tempTxSizeBytes));

			newTransaction = TransactionHelpers.BuildChangelessTransaction(
				wallet,
				keyManager.GetNextChangeKey().GetAssumedScriptPubKey().GetDestinationAddress(network) ?? throw new NullReferenceException("GetDestinationAddress returned null. This should never happen."),
				LabelsArray.Empty,
				cpfpFeeRate,
				new[] { ownOutput },
				tryToSign: true)
				.Transaction;
		}
		else
		{
			if (!transactionToSpeedUp.GetForeignOutputs(keyManager).Any())
			{
				// IF self spend.
				throw new InvalidOperationException("Self spend cannot be sped up.");
			}

			// Else it's RBF.
			isRBF = true;

			var originalFeeRate = transactionToSpeedUp.Transaction.GetFeeRate(transactionToSpeedUp.GetWalletInputs(keyManager).Select(x => x.Coin).Cast<ICoin>().ToArray());
			var originalFee = transactionToSpeedUp.Transaction.GetFee(transactionToSpeedUp.WalletInputs.Select(x => x.Coin).ToArray());
			var minRelayFeeRate = network.CreateTransactionBuilder().StandardTransactionPolicy.MinRelayTxFee ?? new FeeRate(1m);

			// If the highest fee rate is smaller or equal than the original fee rate, then increase fee rate minimally, otherwise built tx with best fee rate.
			//var rbfFeeRate = bestFeeRate is null || bestFeeRate <= originalFeeRate
			//	? new FeeRate(originalFeeRate.SatoshiPerByte + Money.Satoshis(Math.Max(2, originalFeeRate.SatoshiPerByte * 0.05m)).Satoshi)
			//	: bestFeeRate;

			var originalTransaction = transactionToSpeedUp.Transaction;

			// IF send.
			if (ownOutput is not null)
			{
				newTransaction = new SmartTransaction(Transaction.Create(Network.Main), Height.Mempool);
				// IF change present, then we modify the change's amount.
			}
			else
			{
				newTransaction = new SmartTransaction(Transaction.Create(Network.Main), Height.Mempool);
				// IF change not present, then we modify the destination's amount.
				isDestinationAmountModified = true;
			}
		}

		return newTransaction;
	}
}
