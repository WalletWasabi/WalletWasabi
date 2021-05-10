using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
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

		public static bool TryGetNotificationInputs(ProcessedResult result, [NotNullWhen(true)] out string? title, [NotNullWhen(true)] out string? message)
		{
			title = null;
			message = null;

			try
			{
				bool isSpent = result.NewlySpentCoins.Any();
				bool isReceived = result.NewlyReceivedCoins.Any();
				bool isConfirmedReceive = result.NewlyConfirmedReceivedCoins.Any();
				bool isConfirmedSpent = result.NewlyConfirmedReceivedCoins.Any();
				Money miningFee = result.Transaction.Transaction.GetFee(result.SpentCoins.Select(x => x.Coin).ToArray());

				if (isReceived || isSpent)
				{
					Money receivedSum = result.NewlyReceivedCoins.Sum(x => x.Amount);
					Money spentSum = result.NewlySpentCoins.Sum(x => x.Amount);
					Money incoming = receivedSum - spentSum;
					Money receiveSpentDiff = incoming.Abs();
					string amountString = receiveSpentDiff.ToFormattedString();
					message = $"{amountString} BTC";

					if (result.Transaction.Transaction.IsCoinBase)
					{
						title = "Mined";
					}
					else if (isSpent && receiveSpentDiff == miningFee)
					{
						title = "Self Spend";
						message = $"Mining Fee: {amountString} BTC";
					}
					else if (incoming > Money.Zero)
					{
						title = "Transaction Received";
					}
					else if (incoming < Money.Zero)
					{
						title = "Transaction Sent";
					}
				}
				else if (isConfirmedReceive || isConfirmedSpent)
				{
					Money receivedSum = result.ReceivedCoins.Sum(x => x.Amount);
					Money spentSum = result.SpentCoins.Sum(x => x.Amount);
					Money incoming = receivedSum - spentSum;
					Money receiveSpentDiff = incoming.Abs();
					string amountString = receiveSpentDiff.ToFormattedString();
					message = $"{amountString} BTC";

					if (isConfirmedSpent && receiveSpentDiff == miningFee)
					{
						title = "Self Spend Confirmed";
						message = $"Mining Fee: {amountString} BTC";
					}
					else if (incoming > Money.Zero)
					{
						title = "Receive Confirmed";
					}
					else if (incoming < Money.Zero)
					{
						title = "Send Confirmed";
					}
				}
			}
			catch (Exception ex)
			{
				Logger.LogWarning(ex);
			}

			return title is { } && message is { };
		}
	}
}
