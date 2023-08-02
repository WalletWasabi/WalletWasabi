using NBitcoin;
using System.Collections.Generic;
using System.Linq;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Blockchain.Transactions;

namespace WalletWasabi.Blockchain.TransactionBuilding;

public class BuildTransactionResult
{
	public BuildTransactionResult(SmartTransaction transaction, PSBT psbt, bool signed, Money fee, decimal feePercentOfSent, Dictionary<HdPubKey, LabelsArray> hdPubKeysWithNewLabels)
	{
		Transaction = transaction;
		Psbt = psbt;
		Signed = signed;
		Fee = fee;
		FeePercentOfSent = feePercentOfSent;
		HdPubKeysWithNewLabels = hdPubKeysWithNewLabels;
	}

	public SmartTransaction Transaction { get; }
	public PSBT Psbt { get; }
	public bool Signed { get; }
	public Money Fee { get; }
	public decimal FeePercentOfSent { get; }
	public bool SpendsUnconfirmed => Transaction.WalletInputs.Any(c => !c.Confirmed);
	public bool SpendsCoinjoining => Transaction.WalletInputs.Any(c => c.CoinJoinInProgress);
	public IEnumerable<SmartCoin> InnerWalletOutputs => Transaction.WalletOutputs;
	public IEnumerable<SmartCoin> SpentCoins => Transaction.WalletInputs;

	public Dictionary<HdPubKey, LabelsArray> HdPubKeysWithNewLabels { get; }

	public IEnumerable<Coin> OuterWalletOutputs
	{
		get
		{
			var outputs = Transaction.Transaction.Outputs;
			var ownOutputIndexes = Transaction.WalletOutputs.Select(x => x.Index).ToHashSet();
			for (uint i = 0; i < outputs.Count; i++)
			{
				if (!ownOutputIndexes.Contains(i))
				{
					yield return new Coin(Transaction.Transaction, i);
				}
			}
		}
	}
}
