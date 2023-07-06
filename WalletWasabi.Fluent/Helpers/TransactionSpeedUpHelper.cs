using System.Linq;
using NBitcoin;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.Helpers;

internal static class TransactionSpeedUpHelper
{
	public static SmartTransaction CreateSpeedUpTransaction(SmartTransaction transactionToSpeedUp, Wallet wallet)
	{
		var keyManager = wallet.KeyManager;
		var change = transactionToSpeedUp.GetWalletOutputs(keyManager).FirstOrDefault();
		var txSizeBytes = transactionToSpeedUp.Transaction.GetVirtualSize();
		var bestFeeRate = wallet.FeeProvider.AllFeeEstimate?.GetFeeRate(2);

		bool isDestinationAmountModified = false;
		bool isRBF = false;
		SmartTransaction newTransaction;

		if (bestFeeRate is null)
		{
			throw new NullReferenceException($"{nameof(bestFeeRate)} is null. This should never happen.");
		}

		if (transactionToSpeedUp.GetForeignInputs(keyManager).Any() || !transactionToSpeedUp.IsRBF)
		{
			// IF there are any foreign input or doesn't signal RBF, then we can only CPFP.
			isRBF = false;

			if (change is null)
			{
				// IF change is not present, we cannot do anything with it.
				throw new InvalidOperationException("Transaction doesn't signal RBF, nor we have change to CPFP it.");
			}

			// Let's build a CPFP with best fee rate temporarily.
			var tempTx = TransactionHelpers.BuildChangelessTransaction(
				wallet,
				keyManager.GetNextChangeKey().GetAssumedScriptPubKey().GetDestinationAddress(wallet.Network) ?? throw new NullReferenceException("GetDestinationAddress returned null. This should never happen."),
				LabelsArray.Empty,
				bestFeeRate,
				transactionToSpeedUp.GetWalletInputs(keyManager),
				tryToSign: true);
			var tempTxSizeBytes = tempTx.Transaction.Transaction.GetVirtualSize();

			// Let's increase the fee rate of CPFP transaction.
			var cpfpFee = (long)((txSizeBytes + tempTxSizeBytes) * bestFeeRate.SatoshiPerByte) + 1;
			var cpfpFeeRate = new FeeRate((decimal)(cpfpFee / tempTxSizeBytes));

			newTransaction = TransactionHelpers.BuildChangelessTransaction(
				wallet,
				keyManager.GetNextChangeKey().GetAssumedScriptPubKey().GetDestinationAddress(wallet.Network) ?? throw new NullReferenceException("GetDestinationAddress returned null. This should never happen."),
				LabelsArray.Empty,
				cpfpFeeRate,
				transactionToSpeedUp.GetWalletInputs(keyManager),
				tryToSign: true)
				.Transaction;
		}
		else
		{
			// Else it's RBF.
			isRBF = true;

			var originalFeeRate = transactionToSpeedUp.Transaction.GetFeeRate(transactionToSpeedUp.GetWalletInputs(keyManager).Select(x => x.Coin).Cast<ICoin>().ToArray());

			// If the highest fee rate is smaller or equal than the original fee rate, then increase fee rate minimally, otherwise built tx with best fee rate.
			var rbfFeeRate = bestFeeRate is null || bestFeeRate <= originalFeeRate
				? new FeeRate(originalFeeRate.SatoshiPerByte + Money.Satoshis(Math.Max(2, originalFeeRate.SatoshiPerByte * 0.05m)).Satoshi)
				: bestFeeRate;

			var originalTransaction = transactionToSpeedUp.Transaction;

			if (!transactionToSpeedUp.GetForeignOutputs(keyManager).Any())
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

		// TODO: Implement the rest of the logic
		throw new NotImplementedException();
	}
}
