using Microsoft.AspNetCore.HttpOverrides;
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

		var allCoins = wallet.Coins.AsAllCoinsView();
		foreach (SmartCoin coin in allCoins)
		{
			var containingTransaction = coin.Transaction;

			var dateTime = containingTransaction.FirstSeen;
			var found = txRecordList.FirstOrDefault(x => x.TransactionId == coin.TransactionId);
			if (found is { }) // if found then update
			{
				found.DateTime = found.DateTime < dateTime ? found.DateTime : dateTime;
				found.Amount += coin.Amount;
				found.Labels = LabelsArray.Merge(found.Labels, containingTransaction.Labels);
			}
			else
			{
				var outputs = GetOutputs(containingTransaction, wallet.Network).ToList();
				var destinationAddresses = GetDestinationAddresses(outputs);

				txRecordList.Add(new TransactionSummary
				{
					DateTime = dateTime,
					Height = coin.Height,
					Amount = coin.Amount,
					Labels = containingTransaction.Labels,
					TransactionId = coin.TransactionId,
					BlockIndex = containingTransaction.BlockIndex,
					BlockHash = containingTransaction.BlockHash,
					IsOwnCoinjoin = containingTransaction.IsOwnCoinjoin(),
					Inputs = GetInputs(containingTransaction),
					Outputs = outputs,
					DestinationAddresses = destinationAddresses,
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
					var outputs = GetOutputs(spenderTransaction, wallet.Network).ToList();
					var destinationAddresses = GetDestinationAddresses(outputs);

					txRecordList.Add(new TransactionSummary
					{
						DateTime = dateTime,
						Height = spenderTransaction.Height,
						Amount = Money.Zero - coin.Amount,
						Labels = spenderTransaction.Labels,
						TransactionId = spenderTxId,
						BlockIndex = spenderTransaction.BlockIndex,
						BlockHash = spenderTransaction.BlockHash,
						IsOwnCoinjoin = spenderTransaction.IsOwnCoinjoin(),
						Inputs = GetInputs(spenderTransaction),
						Outputs = outputs,
						DestinationAddresses = destinationAddresses,
					});
				}
			}
		}
		txRecordList = txRecordList.OrderByBlockchain().ToList();
		return txRecordList;
	}

	private IEnumerable<BitcoinAddress> GetDestinationAddresses(ICollection<Output> outputs)
	{
		var myOwnNonInternalOutputs = outputs.OfType<OwnOutput>().Where(x => !x.IsInternal).Cast<Output>();
		var foreignOutputs = outputs.OfType<ForeignOutput>().Cast<Output>();

		return myOwnNonInternalOutputs.Concat(foreignOutputs).Select(x => x.DestinationAddress);
	}

	private IEnumerable<Output> GetOutputs(SmartTransaction smartTransaction, Network network)
	{
		var known = smartTransaction.WalletOutputs.Select(coin =>
		{
			var address = coin.TxOut.ScriptPubKey.GetDestinationAddress(network)!;
			return new OwnOutput(coin.TxOut.Value, address, coin.HdPubKey.IsInternal);
		}).Cast<Output>();

		var unknown = smartTransaction.ForeignOutputs.Select(coin =>
		{
			var address = coin.TxOut.ScriptPubKey.GetDestinationAddress(network)!;
			return new ForeignOutput(coin.TxOut.Value, address);
		}).Cast<Output>();

		return known.Concat(unknown);
	}

	private static IEnumerable<IInput> GetInputs(SmartTransaction transaction)
	{
		var known = transaction.WalletInputs
			.Select(x => new KnownInput(x.Amount))
			.OfType<IInput>();

		var unknown = transaction.ForeignInputs
			.Select(_ => new ForeignInput())
			.OfType<IInput>();

		return known.Concat(unknown);
	}
}
