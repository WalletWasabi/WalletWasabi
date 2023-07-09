using NBitcoin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Wallets;

namespace WalletWasabi.Blockchain.TransactionBuilding;

public static class TransactionModifierWalletExtensions
{
	public static BuildTransactionResult CancelTransaction(
		this Wallet wallet,
		SmartTransaction transactionToCancel)
	{
		var keyManager = wallet.KeyManager;
		var network = wallet.Network;

		if (transactionToCancel.GetWalletInputs(keyManager).Count() != transactionToCancel.Transaction.Inputs.Count)
		{
			throw new InvalidOperationException("Transaction has foreign inputs. Cannot cancel.");
		}

		if (transactionToCancel.GetWalletOutputs(keyManager).Count() == transactionToCancel.Transaction.Outputs.Count)
		{
			throw new InvalidOperationException("Transaction has no foreign outputs. Cannot cancel.");
		}

		// Take the first own output and if we have it that's where we send all our money to.
		var ownOutput = transactionToCancel.GetWalletOutputs(keyManager).FirstOrDefault();

		// Calculate the original fee rate and fee.
		var originalFeeRate = transactionToCancel.Transaction.GetFeeRate(transactionToCancel.GetWalletInputs(keyManager).Select(x => x.Coin).Cast<ICoin>().ToArray());
		var originalFee = transactionToCancel.Transaction.GetFee(transactionToCancel.WalletInputs.Select(x => x.Coin).ToArray());
		var minRelayFeeRate = network.CreateTransactionBuilder().StandardTransactionPolicy.MinRelayTxFee ?? new FeeRate(1m);

		BuildTransactionResult cancelTransaction;
		int i = 1;
		do
		{
			cancelTransaction = wallet.BuildChangelessTransaction(
				ownOutput?.Coin.ScriptPubKey.GetDestinationAddress(network) ?? keyManager.GetNextChangeKey().GetAssumedScriptPubKey().GetDestinationAddress(network) ?? throw new NullReferenceException("GetDestinationAddress returned null. This should never happen."),
				LabelsArray.Empty,
				new FeeRate(originalFeeRate.SatoshiPerByte + i),
				transactionToCancel.WalletInputs,
				allowDoubleSpend: true,
				tryToSign: true);

			// Double i, so we should be able to find a suitable cancel tx in a few iterations.
			i *= 2;

			// https://github.com/bitcoin/bips/blob/master/bip-0125.mediawiki
			// The replacement transaction must also pay for its own bandwidth at or above the rate set by the node's minimum relay fee setting. For example, if the minimum relay fee is 1 satoshi/byte and the replacement transaction is 500 bytes total, then the replacement must pay a fee at least 500 satoshis higher than the sum of the originals.
		}
		while (originalFee + Money.Satoshis(minRelayFeeRate.SatoshiPerByte * cancelTransaction.Transaction.Transaction.GetVirtualSize()) >= cancelTransaction.Fee);

		return cancelTransaction;
	}

	public static BuildTransactionResult SpeedUpTransaction(
		this Wallet wallet,
		SmartTransaction transactionToSpeedUp)
	{
		var keyManager = wallet.KeyManager;
		var network = wallet.Network;

		// Take the largest own output and if we have it that's what we will want to CPFP or deduct RBF fee from.
		var ownOutput = transactionToSpeedUp.GetWalletOutputs(keyManager).OrderByDescending(x => x.Amount).FirstOrDefault();
		var txSizeBytes = transactionToSpeedUp.Transaction.GetVirtualSize();

		BuildTransactionResult newTransaction;

		var bestFeeRate = wallet.FeeProvider.AllFeeEstimate?.GetFeeRate(2);
		if (bestFeeRate is null)
		{
			throw new NullReferenceException($"{nameof(bestFeeRate)} is null. This should never happen.");
		}

		// We cannot RBF if we
		// - have foreign inputs,
		// - the transaction doesn't signal RBF.
		// We should not RBF if
		// - any of the transaction's outputs are spent.
		if (transactionToSpeedUp.GetForeignInputs(keyManager).Any() || !transactionToSpeedUp.IsRBF || transactionToSpeedUp.WalletOutputs.Any(x => x.IsSpent()))
		{
			// IF there are any foreign input or doesn't signal RBF, then we can only CPFP.
			if (ownOutput is null)
			{
				// IF change is not present, we cannot do anything with it.
				throw new InvalidOperationException("Transaction doesn't signal RBF, nor we have change to CPFP it.");
			}

			// Let's build a CPFP with best fee rate temporarily.
			var tempTx = wallet.BuildChangelessTransaction(
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

			newTransaction = wallet.BuildChangelessTransaction(
				keyManager.GetNextChangeKey().GetAssumedScriptPubKey().GetDestinationAddress(network) ?? throw new NullReferenceException("GetDestinationAddress returned null. This should never happen."),
				LabelsArray.Empty,
				cpfpFeeRate,
				new[] { ownOutput },
				tryToSign: true);
		}
		else
		{
			if (!transactionToSpeedUp.GetForeignOutputs(keyManager).Any())
			{
				// IF self spend.
				throw new InvalidOperationException("Self spend cannot be sped up.");
			}

			// Else it's RBF.
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

			// If we have no own output, then we substract the fee from the largest foreign output.
			var largestForeignOuput = foreignOutputs.First();
			var largestForeignOuputDestReq = new DestinationRequest(
				scriptPubKey: largestForeignOuput.TxOut.ScriptPubKey,
				amount: largestForeignOuput.TxOut.Value,
				subtractFee: ownOutput is null,
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
				tryToSign: true);
		}

		return newTransaction;
	}
}
