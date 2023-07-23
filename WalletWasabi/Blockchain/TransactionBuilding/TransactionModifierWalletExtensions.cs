using Microsoft.AspNetCore.DataProtection.KeyManagement;
using NBitcoin;
using System.Collections.Generic;
using System.Linq;
using WabiSabi.Crypto.Randomness;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Extensions;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;
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

		if (!transactionToCancel.IsCancelable(keyManager))
		{
			throw new InvalidOperationException("Transaction is not cancelable.");
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

		cancelTransaction.Transaction.SetCancellation();

		if (transactionToCancel.IsSpeedup)
		{
			cancelTransaction.Transaction.SetSpeedup();
		}

		var transactionToCancelFee = transactionToCancel.Transaction.GetFee(transactionToCancel.WalletInputs.Select(x => x.Coin).ToArray());
		var transactionToCancelSentAmount = transactionToCancel.GetForeignOutputs(keyManager).Sum(x => x.TxOut.Value);
		var transactionToCancelCost = transactionToCancelSentAmount + transactionToCancelFee;
		if (transactionToCancelCost < cancelTransaction.Fee)
		{
			throw new InvalidOperationException($"It'd cost more to cancel this transaction ({cancelTransaction.Fee}), than it costs to let this transaction happen ({transactionToCancelCost}).");
		}

		return cancelTransaction;
	}

	public static BuildTransactionResult SpeedUpTransaction(
		this Wallet wallet,
		SmartTransaction transactionToSpeedUp)
	{
		var keyManager = wallet.KeyManager;

		if (transactionToSpeedUp.IsRbfable(keyManager))
		{
			try
			{
				return wallet.RbfTransaction(transactionToSpeedUp);
			}
			catch (Exception rbfEx)
			{
				try
				{
					return wallet.CpfpTransaction(transactionToSpeedUp);
				}
				catch
				{
					// If CPFP fails as well, then we throw the original exception, since it was supposed to be an RBF to begin with.
					throw rbfEx;
				}
			}
		}
		else if (transactionToSpeedUp.IsCpfpable(keyManager))
		{
			return wallet.CpfpTransaction(transactionToSpeedUp);
		}
		else
		{
			throw new InvalidOperationException("Transaction is not speedupable.");
		}
	}

	private static BuildTransactionResult RbfTransaction(this Wallet wallet, SmartTransaction transactionToSpeedUp)
	{
		var keyManager = wallet.KeyManager;
		var network = wallet.Network;

		var bestFeeRate = wallet.FeeProvider.AllFeeEstimate?.GetFeeRate(2) ?? throw new NullReferenceException($"Couldn't get fee rate. This should never happen.");

		var txSizeBytes = transactionToSpeedUp.Transaction.GetVirtualSize();
		var originalFeeRate = transactionToSpeedUp.Transaction.GetFeeRate(transactionToSpeedUp.GetWalletInputs(keyManager).Select(x => x.Coin).Cast<ICoin>().ToArray());
		var originalFee = transactionToSpeedUp.Transaction.GetFee(transactionToSpeedUp.WalletInputs.Select(x => x.Coin).ToArray());
		var minRelayFeeRate = network.CreateTransactionBuilder().StandardTransactionPolicy.MinRelayTxFee ?? new FeeRate(1m);
		var minRelayFee = originalFee + Money.Satoshis(minRelayFeeRate.SatoshiPerByte * txSizeBytes);
		var minimumRbfFeeRate = new FeeRate(minRelayFee, txSizeBytes);

		// If the best fee rate is smaller than the minimum bump or not available, then we go with the minimum bump.
		var rbfFeeRate = (bestFeeRate is null || bestFeeRate <= minimumRbfFeeRate)
			? minimumRbfFeeRate
			: bestFeeRate;

		// Take the largest own output and if we have it that's what we will want to deduct RBF fee from.
		var ownOutput = transactionToSpeedUp.GetWalletOutputs(keyManager).OrderByDescending(x => x.Amount).FirstOrDefault();

		// IF change present, then we modify the change's amount.
		var payments = new List<DestinationRequest>();
		foreach (var coin in transactionToSpeedUp.GetWalletOutputs(keyManager))
		{
			DestinationRequest destReq;
			if (coin == ownOutput)
			{
				destReq = new DestinationRequest(
					scriptPubKey: coin.ScriptPubKey,
					MoneyRequest.CreateAllRemaining(subtractFee: true),
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

		if (foreignOutputs.Any())
		{
			// If we have no own output, then we substract the fee from the largest foreign output.
			var largestForeignOuput = foreignOutputs.First();
			var largestForeignOuputDestReq = new DestinationRequest(
				scriptPubKey: largestForeignOuput.TxOut.ScriptPubKey,
				ownOutput is null
					? MoneyRequest.CreateAllRemaining(subtractFee: true)
					: MoneyRequest.Create(largestForeignOuput.TxOut.Value),
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
		}

		var allowedInputs = transactionToSpeedUp.WalletInputs.Select(coin => coin.Outpoint);

		BuildTransactionResult rbf = wallet.BuildTransaction(
			password: wallet.Kitchen.SaltSoup(),
			payments: new PaymentIntent(payments),
			feeStrategy: FeeStrategy.CreateFromFeeRate(rbfFeeRate),
			allowUnconfirmed: true,
			allowedInputs: allowedInputs,
			allowDoubleSpend: true,
			tryToSign: true);

		if (transactionToSpeedUp.IsCancellation)
		{
			rbf.Transaction.SetCancellation();
		}

		if (transactionToSpeedUp.IsCPFP)
		{
			// If we're RBF-ing a CPFP which has a parent with multiple own outputs and only spends one of them, then the maxFee could be higher.
			AssertMaxCpfpFee(transactionToSpeedUp, rbf, keyManager);
		}
		else
		{
			// We want to ensure that the RBF's fee is not too high.
			var rbfFee = rbf.Fee.Satoshi;
			var maxFee = rbf.Transaction.Transaction.Outputs.Sum(x => x.Value);

			if (rbfFee > maxFee)
			{
				throw new InvalidOperationException($"RBF fee ({rbfFee}) is higher than the total output amount in this transaction ({maxFee}).");
			}
		}

		rbf.Transaction.SetSpeedup();

		return rbf;
	}

	public static BuildTransactionResult CpfpTransaction(this Wallet wallet, SmartTransaction transactionToCpfp)
	{
		var keyManager = wallet.KeyManager;
		var ownOutput = transactionToCpfp.GetWalletOutputs(keyManager).Where(x => !x.IsSpent()).OrderByDescending(x => x.Amount).FirstOrDefault() ?? throw new InvalidOperationException($"Can't CPFP: transaction has no unspent wallet output.");
		List<SmartCoin> allowedInputs = new()
		{
			ownOutput
		};

		try
		{
			return wallet.CpfpTransaction(transactionToCpfp, allowedInputs);
		}
		catch (Exception ex)
		{
			// It might be that the change is too small to CPFP, so we try to add another input.
			// Let's only do this once, because the more we try to merge the more problematic it'll get from privacy point of view.
			var remainingCoins = wallet.Coins
				.Except(allowedInputs)
				.Where(x => x.HdPubKey.Labels == ownOutput.HdPubKey.Labels || x.IsPrivate(wallet.AnonScoreTarget))
				.OrderByDescending(x => x.Confirmed)
				.ThenByDescending(x => x.Amount);

			if (remainingCoins.Any())
			{
				Logger.LogDebug(ex);

				allowedInputs.Add(remainingCoins.BiasedRandomElement(80, InsecureRandom.Instance)!);

				return wallet.CpfpTransaction(transactionToCpfp, allowedInputs);
			}

			throw;
		}
	}

	public static BuildTransactionResult CpfpTransaction(this Wallet wallet, SmartTransaction transactionToCpfp, IEnumerable<SmartCoin> allowedInputs)
	{
		var keyManager = wallet.KeyManager;
		var network = wallet.Network;

		// Take the largest unspent own output and if we have it that's what we will want to CPFP.
		var txSizeBytes = transactionToCpfp.Transaction.GetVirtualSize();

		var bestFeeRate = wallet.FeeProvider.AllFeeEstimate?.GetFeeRate(2) ?? throw new NullReferenceException($"Couldn't get fee rate. This should never happen.");

		// Let's build a CPFP with best fee rate temporarily.
		var tempTx = wallet.BuildChangelessTransaction(
			keyManager.GetNextChangeKey().GetAssumedScriptPubKey().GetDestinationAddress(network) ?? throw new NullReferenceException("GetDestinationAddress returned null. This should never happen."),
			LabelsArray.Empty,
			bestFeeRate,
			allowedInputs,
			tryToSign: true);
		var tempTxSizeBytes = tempTx.Transaction.Transaction.GetVirtualSize();

		// Let's increase the fee rate of CPFP transaction.
		// Let's assume the transaction we want to CPFP pays 0 fees.
		var cpfpFee = (long)((txSizeBytes + tempTxSizeBytes) * bestFeeRate.SatoshiPerByte) + 1;
		var cpfpFeeRate = new FeeRate((decimal)(cpfpFee / tempTxSizeBytes));

		var cpfp = wallet.BuildChangelessTransaction(
			keyManager.GetNextChangeKey().GetAssumedScriptPubKey().GetDestinationAddress(network) ?? throw new NullReferenceException("GetDestinationAddress returned null. This should never happen."),
			LabelsArray.Empty,
			cpfpFeeRate,
			allowedInputs,
			tryToSign: true);

		cpfp.Transaction.SetSpeedup();

		AssertMaxCpfpFee(transactionToCpfp, cpfp, keyManager);

		return cpfp;
	}

	private static void AssertMaxCpfpFee(SmartTransaction transactionToCpfp, BuildTransactionResult cpfp, KeyManager keyManager)
	{
		// We want to ensure that the CPFP's fee is not too high.
		var outputsToCpfpSum = transactionToCpfp.GetWalletOutputs(keyManager).Sum(x => x.Amount);

		var spentOutputSum = transactionToCpfp.GetWalletOutputs(keyManager).Where(x => cpfp.Transaction.GetWalletInputs(keyManager).Contains(x)).Sum(x => x.Amount);
		var totalReceivedOutputSum = transactionToCpfp.GetWalletOutputs(keyManager).Sum(x => x.Amount);
		var totalSentOutputSum = transactionToCpfp.GetForeignOutputs(keyManager).Sum(x => x.TxOut.Value);

		var cpfpFee = cpfp.Fee.Satoshi;
		var maxFee = (totalReceivedOutputSum - spentOutputSum) + (outputsToCpfpSum - cpfpFee);
		if (transactionToCpfp.GetWalletInputs(keyManager).Any())
		{
			// If we have inputs in the transaction, then we might be sending money to others.
			// If we don't have inputs in the transaction, then we are receiving money from others.
			maxFee += totalSentOutputSum;
		}

		if (cpfpFee > maxFee)
		{
			throw new InvalidOperationException($"CPFP fee ({cpfpFee}) is higher than what it it's worth to speed up this transaction ({maxFee}).");
		}
	}
}
