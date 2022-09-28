using NBitcoin;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Extensions;
using WalletWasabi.Wallets;

namespace WalletWasabi.Blockchain.Transactions.Summary;

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
					Outputs = GetOutputs(containingTransaction, wallet.Network),
					VirtualSize = containingTransaction.Transaction.GetVirtualSize(),
					Version = (int)containingTransaction.Transaction.Version,
					BlockTime = containingTransaction.FirstSeen.ToUnixTimeSeconds(),
					Size = containingTransaction.Transaction.GetSerializedSize(),
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
						Amount = coin.Amount,
						Label = spenderTransaction.Label,
						TransactionId = spenderTxId,
						BlockIndex = spenderTransaction.BlockIndex,
						BlockHash = spenderTransaction.BlockHash,
						IsOwnCoinjoin = spenderTransaction.IsOwnCoinjoin(),
						Inputs = GetInputs(wallet.Network, containingTransaction),
						Outputs = GetOutputs(spenderTransaction, wallet.Network),
						VirtualSize = spenderTransaction.Transaction.GetVirtualSize(),
						Version = (int)spenderTransaction.Transaction.Version,
						BlockTime = spenderTransaction.FirstSeen.ToUnixTimeSeconds(),
						Size = spenderTransaction.Transaction.GetSerializedSize(),
					});
				}
			}
		}

		txRecordList = txRecordList.OrderByBlockchain().ToList();
		return txRecordList;
	}

	private static IEnumerable<Output> GetOutputs(SmartTransaction smartTransaction, Network network)
	{
		var txOutList = smartTransaction.Transaction.Outputs.Select(
			txOut =>
			{
				var amount = txOut.Value;
				var address = txOut.ScriptPubKey.GetDestinationAddress(network);
				var coin = smartTransaction.WalletOutputs.FirstOrDefault(smartCoin => smartCoin.TxOut == txOut);
				bool isSpent = true;
				if (coin != null)
				{
					isSpent = coin.SpenderTransaction != null;
				}

				return new Output(amount, address, isSpent);
			});

		return txOutList;

		return smartTransaction.WalletOutputs.Select(x => new Output(x.Amount, x.ScriptPubKey.GetDestinationAddress(network), x.SpenderTransaction is not null));
	}

	private static IEnumerable<Input> GetInputs(Network network, SmartTransaction transaction)
	{
		var known = transaction.WalletInputs.Select(x => (Input)new InputAmount(x.Amount, x.ScriptPubKey.GetDestinationAddress(network)));
		var unknown = transaction.ForeignInputs.Select(x => (Input)new UnknownInput(x.Transaction.GetHash()));

		return known.Concat(unknown);
	}
}
