using NBitcoin;
using System.Collections.Generic;
using System.Linq;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Blockchain.Transactions.Summary;
using WalletWasabi.Extensions;
using WalletWasabi.Wallets;

namespace WalletWasabi.Blockchain.Transactions;

public class TransactionHistoryBuilder
{
	public TransactionHistoryBuilder(Wallet wallet)
	{
		Wallet = wallet;
	}

	public Wallet Wallet { get; }

	public List<TransactionSummary> BuildHistorySummary()
	{
		var wallet = Wallet;

		var txRecordList = new List<TransactionSummary>();
		if (wallet is null)
		{
			return txRecordList;
		}

		var allCoins = ((CoinsRegistry)wallet.Coins).AsAllCoinsView();
		foreach (SmartCoin coin in allCoins)
		{
			var containingTransaction = coin.Transaction;

			var dateTime = containingTransaction.FirstSeen;
			var found = txRecordList.FirstOrDefault(x => x.TransactionId == coin.TransactionId);
			if (found is { }) // if found then update
			{
				found.DateTime = found.DateTime < dateTime ? found.DateTime : dateTime;
				found.Amount += coin.Amount;
				found.Label = SmartLabel.Merge(found.Label, containingTransaction.Label);
			}
			else
			{
				txRecordList.Add(new TransactionSummary
				{
					DateTime = dateTime,
					Height = coin.Height,
					Amount = coin.Amount,
					Label = containingTransaction.Label,
					TransactionId = coin.TransactionId,
					BlockIndex = containingTransaction.BlockIndex,
					BlockHash = containingTransaction.BlockHash,
					IsOwnCoinjoin = containingTransaction.IsOwnCoinjoin(),
					Inputs = GetInputs(wallet.Network, containingTransaction),
					Outputs = GetOutputs(containingTransaction),
				});
			}

			var spenderTransaction = coin.SpenderTransaction;
			if (spenderTransaction is { })
			{
				var spenderTxId = spenderTransaction.GetHash();
				dateTime = spenderTransaction.FirstSeen;
				var foundSpenderCoin = txRecordList.FirstOrDefault(x => x.TransactionId == spenderTxId);
				if (foundSpenderCoin is { }) // if found
				{
					foundSpenderCoin.DateTime = foundSpenderCoin.DateTime < dateTime ? foundSpenderCoin.DateTime : dateTime;
					foundSpenderCoin.Amount -= coin.Amount;
				}
				else
				{
					txRecordList.Add(new TransactionSummary
					{
						DateTime = dateTime,
						Height = spenderTransaction.Height,
						Amount = Money.Zero - coin.Amount,
						Label = spenderTransaction.Label,
						TransactionId = spenderTxId,
						BlockIndex = spenderTransaction.BlockIndex,
						BlockHash = spenderTransaction.BlockHash,
						IsOwnCoinjoin = spenderTransaction.IsOwnCoinjoin(),
						Inputs = GetInputs(wallet.Network, spenderTransaction),
						Outputs = GetOutputs(spenderTransaction),
					});
				}
			}
		}
		txRecordList = txRecordList.OrderByBlockchain().ToList();
		return txRecordList;
	}

	private IEnumerable<IOutput> GetOutputs(SmartTransaction transaction)
	{
		var known = transaction.WalletOutputs.Select(coin => (IOutput)new KnownOutput(coin, Wallet.Network));
		var unknown = transaction.ForeignOutputs.Select(txOut => (IOutput)new UnknownOutput(txOut, Wallet.Network));

		return known.Concat(unknown);
	}

	private static IEnumerable<IInput> GetInputs(Network network, SmartTransaction transaction)
	{
		var known = transaction.WalletInputs.Select(coin => (IInput)new KnownInput(coin, network));
		var unknown = transaction.ForeignInputs.Select(x => (IInput)new ForeignInput(x.Transaction.GetHash()));

		return known.Concat(unknown);
	}
}
