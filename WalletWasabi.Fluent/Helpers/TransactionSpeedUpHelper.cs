using System.Collections.Generic;
using System.Linq;
using NBitcoin;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Blockchain.TransactionBuilding;
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
			var minRelayFee = originalFee + Money.Satoshis(minRelayFeeRate.SatoshiPerByte * txSizeBytes);
			var minimumRbfFeeRate = new FeeRate(minRelayFee, txSizeBytes);

			// If the best fee rate is smaller than the minimum bump or not available, then we go with the minimum bump.
			var rbfFeeRate = (bestFeeRate is null || bestFeeRate <= minimumRbfFeeRate)
				? minimumRbfFeeRate
				: bestFeeRate;

			// IF change present, then we modify the change's amount.
			var payments = new List<DestinationRequest>();

			foreach (var coin in transactionToSpeedUp.GetWalletOutputs(keyManager))
			{
				DestinationRequest destReq;
				if (coin == ownOutput)
				{
					destReq = new DestinationRequest(
						scriptPubKey: coin.ScriptPubKey,
						amount: coin.Amount,
						subtractFee: true,
						labels: coin.HdPubKey.Labels);
				}
				else
				{
					destReq = new DestinationRequest(
						scriptPubKey: coin.ScriptPubKey,
						amount: coin.Amount,
						subtractFee: false,
						labels: coin.HdPubKey.Labels);
				}

				payments.Add(destReq);
			}

			var foreignOutputs = transactionToSpeedUp.GetForeignOutputs(keyManager).OrderByDescending(x => x.TxOut.Value).ToArray();

			var haveOwnOutput = ownOutput is not null;
			if (haveOwnOutput)
			{
				isDestinationAmountModified = true;
			}

			// If we have no own output, then we substract the fee from the largest foreign output.
			var largestForeignOuput = foreignOutputs.First();
			var largestForeignOuputDestReq = new DestinationRequest(
				scriptPubKey: largestForeignOuput.TxOut.ScriptPubKey,
				amount: largestForeignOuput.TxOut.Value,
				subtractFee: !haveOwnOutput,
				labels: transactionToSpeedUp.Labels);
			payments.Add(largestForeignOuputDestReq);

			foreach (var output in foreignOutputs.Skip(1))
			{
				var destReq = new DestinationRequest(
					scriptPubKey: output.TxOut.ScriptPubKey,
					amount: output.TxOut.Value,
					subtractFee: false,
					labels: transactionToSpeedUp.Labels);

				payments.Add(destReq);
			}

			newTransaction = wallet.BuildTransaction(
				password: wallet.Kitchen.SaltSoup(),
				payments: new PaymentIntent(payments),
				feeStrategy: FeeStrategy.CreateFromFeeRate(rbfFeeRate),
				allowUnconfirmed: true,
				allowedInputs: transactionToSpeedUp.WalletInputs.Select(coin => coin.Outpoint),
				allowDoubleSpend: true,
				tryToSign: true)
				.Transaction;
		}

		return newTransaction;
	}
}
