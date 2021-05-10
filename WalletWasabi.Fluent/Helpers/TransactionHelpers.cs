using System;
using System.Collections.Generic;
using System.Linq;
using NBitcoin;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.TransactionBuilding;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Blockchain.TransactionProcessing;
using WalletWasabi.Fluent.ViewModels.Wallets.Send;
using WalletWasabi.Logging;
using WalletWasabi.Wallets;
using WalletWasabi.WebClients.PayJoin;

namespace WalletWasabi.Fluent.Helpers
{
	public static class TransactionHelpers
	{
		public static BuildTransactionResult BuildTransaction(Wallet wallet, BitcoinAddress address, Money amount, SmartLabel labels, FeeRate feeRate, IEnumerable<SmartCoin> coins, bool subtractFee, IPayjoinClient? payJoinClient = null)
		{
			if (payJoinClient is { } && subtractFee)
			{
				throw new InvalidOperationException("Not possible to subtract the fee.");
			}

			var intent = new PaymentIntent(
				destination: address,
				amount: amount,
				subtractFee: subtractFee,
				label: labels);

			var txRes = wallet.BuildTransaction(
				password: wallet.Kitchen.SaltSoup(),
				payments: intent,
				feeStrategy: FeeStrategy.CreateFromFeeRate(feeRate),
				allowUnconfirmed: true,
				allowedInputs: coins.Select(coin => coin.OutPoint),
				payjoinClient: payJoinClient);

			return txRes;
		}

		public static BuildTransactionResult BuildTransaction(Wallet wallet, TransactionInfo transactionInfo, bool subtractFee = false, bool isPayJoin = false)
		{
			if (isPayJoin && subtractFee)
			{
				throw new InvalidOperationException("Not possible to subtract the fee.");
			}

			return BuildTransaction(
				wallet,
				transactionInfo.Address,
				transactionInfo.Amount,
				transactionInfo.Labels,
				transactionInfo.FeeRate,
				transactionInfo.Coins,
				subtractFee,
				isPayJoin ? transactionInfo.PayJoinClient : null);

		}

		public static (string?, string?) GetNotificationInputs(ProcessedResult e)
		{
			try
			{
				// ToDo
				// Double spent.
				// Anonymity set gained?
				// Received dust

				bool isSpent = e.NewlySpentCoins.Any();
				bool isReceived = e.NewlyReceivedCoins.Any();
				bool isConfirmedReceive = e.NewlyConfirmedReceivedCoins.Any();
				bool isConfirmedSpent = e.NewlyConfirmedReceivedCoins.Any();
				Money miningFee = e.Transaction.Transaction.GetFee(e.SpentCoins.Select(x => x.Coin).ToArray());

				if (isReceived || isSpent)
				{
					Money receivedSum = e.NewlyReceivedCoins.Sum(x => x.Amount);
					Money spentSum = e.NewlySpentCoins.Sum(x => x.Amount);
					Money incoming = receivedSum - spentSum;
					Money receiveSpentDiff = incoming.Abs();
					string amountString = receiveSpentDiff.ToString(false, true);

					if (e.Transaction.Transaction.IsCoinBase)
					{
						return ("Mined", $"{amountString} BTC");
					}
					else if (isSpent && receiveSpentDiff == miningFee)
					{
						return ("Self Spend", $"Mining Fee: {amountString} BTC");
					}
					else if (incoming > Money.Zero)
					{
						if (e.Transaction.IsRBF && e.Transaction.IsReplacement)
						{
							return ("Received Replaceable Replacement Transaction", $"{amountString} BTC");
						}
						else if (e.Transaction.IsRBF)
						{
							return ("Received Replaceable Transaction", $"{amountString} BTC");
						}
						else if (e.Transaction.IsReplacement)
						{
							return ("Received Replacement Transaction", $"{amountString} BTC");
						}
						else
						{
							return ("Received", $"{amountString} BTC");
						}
					}
					else if (incoming < Money.Zero)
					{
						return ("Sent", $"{amountString} BTC");
					}
				}
				else if (isConfirmedReceive || isConfirmedSpent)
				{
					Money receivedSum = e.ReceivedCoins.Sum(x => x.Amount);
					Money spentSum = e.SpentCoins.Sum(x => x.Amount);
					Money incoming = receivedSum - spentSum;
					Money receiveSpentDiff = incoming.Abs();
					string amountString = receiveSpentDiff.ToString(false, true);

					if (isConfirmedSpent && receiveSpentDiff == miningFee)
					{
						return ("Self Spend Confirmed", $"Mining Fee: {amountString} BTC");
					}
					else if (incoming > Money.Zero)
					{
						return ("Receive Confirmed", $"{amountString} BTC");
					}
					else if (incoming < Money.Zero)
					{
						return ("Send Confirmed", $"{amountString} BTC");
					}
				}
			}
			catch (Exception ex)
			{
				Logger.LogWarning(ex);
			}

			return (null, null);
		}
	}
}
