using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NBitcoin;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.TransactionBuilding;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Fluent.ViewModels.Wallets.Send;
using WalletWasabi.Models;
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

		public static async Task<SmartTransaction> ParseTransactionAsync(string path, Network network)
		{
			var psbtBytes = await File.ReadAllBytesAsync(path);
			PSBT psbt;

			try
			{
				psbt = PSBT.Load(psbtBytes, network);
			}
			catch
			{
				var text = await File.ReadAllTextAsync(path);
				text = text.Trim();
				try
				{
					psbt = PSBT.Parse(text, network);
				}
				catch
				{
					return new SmartTransaction(Transaction.Parse(text, network), Height.Unknown);
				}
			}

			if (!psbt.IsAllFinalized())
			{
				psbt.Finalize();
			}

			return psbt.ExtractSmartTransaction();
		}
	}
}
