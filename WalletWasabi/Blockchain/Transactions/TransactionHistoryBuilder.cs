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
				txRecordList.Add(ToSummary(containingTransaction, coin, wallet));
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
					txRecordList.Add(ToSummary(spenderTransaction, coin, wallet));
				}
			}
		}

		txRecordList = txRecordList.OrderByBlockchain().ToList();
		return txRecordList;
	}

	private static TransactionSummary ToSummary(SmartTransaction transaction, SmartCoin coin, Wallet wallet)
	{
		var outputs = GetOutputs(transaction, wallet.Network).ToList();

		return new TransactionSummary
		{
			DateTime = transaction.FirstSeen,
			Height = coin.Height,
			Amount = coin.Amount,
			Label = transaction.Label,
			TransactionId = coin.TransactionId,
			BlockIndex = transaction.BlockIndex,
			BlockHash = transaction.BlockHash,
			IsOwnCoinjoin = transaction.IsOwnCoinjoin(),
			Inputs = GetInputs(wallet.Network, transaction),
			Outputs = outputs,
			Version = (int)transaction.Transaction.Version,
			BlockTime = transaction.FirstSeen.ToUnixTimeSeconds(),
			Size = transaction.Transaction.GetSerializedSize(),
			VirtualSize = transaction.Transaction.GetVirtualSize(),
			//Weight = ??
		};
	}

	private static IEnumerable<Output> GetOutputs(SmartTransaction smartTransaction, Network network)
	{
		var txOutList = smartTransaction.Transaction.Outputs.Select(
			txOut =>
			{
				var amount = txOut.Value;
				var address = txOut.ScriptPubKey.GetDestinationAddress(network);
				var associatedCoin = smartTransaction.WalletOutputs.FirstOrDefault(smartCoin => smartCoin.TxOut == txOut);
				var features = GetFeatures(txOut, associatedCoin);
				return new Output(amount, address, associatedCoin?.IsSpent() ?? false, features);
			});

		return txOutList;
	}

	private static IEnumerable<Feature> GetFeatures(TxOut txOut, SmartCoin? associatedCoin)
	{
		if (associatedCoin != null && associatedCoin.IsReplaceable())
		{
			yield return Feature.RBF;
		}

		yield return txOut.ScriptPubKey.IsScriptType(ScriptType.Taproot) ? Feature.Taproot : Feature.SegWit;
	}

	private static IEnumerable<Input> GetInputs(Network network, SmartTransaction transaction)
	{
		var known = transaction.WalletInputs.Select(x => (Input)new InputAmount(x.Amount, x.ScriptPubKey.GetDestinationAddress(network)));
		var unknown = transaction.ForeignInputs.Select(x => (Input)new UnknownInput(x.Transaction.GetHash()));

		return known.Concat(unknown);
	}
}
