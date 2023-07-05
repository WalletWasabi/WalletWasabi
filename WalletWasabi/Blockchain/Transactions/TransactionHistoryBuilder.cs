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
				found.Labels = LabelsArray.Merge(found.Labels, containingTransaction.Labels);
			}
			else
			{
				var outputs = GetOutputs(containingTransaction, wallet.Network).ToList();
				var inputs = GetInputs(containingTransaction).ToList();
				var destinationAddresses = GetDestinationAddresses(inputs, outputs);

				txRecordList.Add(new TransactionSummary
				{
					Transaction = containingTransaction,
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
					var inputs = GetInputs(containingTransaction).ToList();
					var destinationAddresses = GetDestinationAddresses(inputs, outputs);

					txRecordList.Add(new TransactionSummary
					{
						Transaction = spenderTransaction,
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

	private IEnumerable<BitcoinAddress> GetDestinationAddresses(ICollection<IInput> inputs, ICollection<Output> outputs)
	{
		var myOwnInputs = inputs.OfType<KnownInput>().ToList();
		var foreignInputs = inputs.OfType<ForeignInput>().ToList();
		var myOwnOutputs = outputs.OfType<OwnOutput>().ToList();
		var foreignOutputs = outputs.OfType<ForeignOutput>().ToList();
		
		// All inputs and outputs are my own, transaction is a self-spend.
		if (!foreignInputs.Any() && !foreignOutputs.Any())
		{
			// Classic self-spend to one or more external addresses.
			if (myOwnOutputs.Any(x => !x.IsInternal))
			{
				// Destinations are the external addresses.
				return myOwnOutputs.Where(x => !x.IsInternal).Select(x => x.DestinationAddress);
			}

			// Edge-case: self-spend to one or more internal addresses.
			// We can't know the destinations, return all the outputs.
			return myOwnOutputs.Select(x => x.DestinationAddress);
		}

		// All inputs are foreign but some outputs are my own, someone is sending coins to me.
		if (!myOwnInputs.Any() && myOwnOutputs.Any())
		{
			// All outputs that are my own are the destinations.
			return myOwnOutputs.Select(x => x.DestinationAddress);
		}
		
		// I'm sending a transaction to someone else.
		// All outputs that are not my own are the destinations.
		return foreignOutputs.Select(x => x.DestinationAddress);
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
