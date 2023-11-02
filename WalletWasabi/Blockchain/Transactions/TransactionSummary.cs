using NBitcoin;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Models;

namespace WalletWasabi.Blockchain.Transactions;

public class TransactionSummary
{
	public TransactionSummary(SmartTransaction tx, Money amount, Func<Money> feeFetcher)
	{
		Transaction = tx;
		Amount = amount;
		GetFee = feeFetcher;
	}

	public SmartTransaction Transaction { get; }
	public Money Amount { get; set; }
	public DateTimeOffset FirstSeen => Transaction.FirstSeen;
	public LabelsArray Labels => Transaction.Labels;
	public Height Height => Transaction.Height;
	public uint256? BlockHash => Transaction.BlockHash;
	public int BlockIndex => Transaction.BlockIndex;
	public bool IsCancellation => Transaction.IsCancellation;
	public bool IsSpeedup => Transaction.IsSpeedup;
	public bool IsCPFP => Transaction.IsCPFP;
	public bool IsCPFPd => Transaction.IsCPFPd;
	public Func<Money> GetFee { get; }

	public uint256 GetHash() => Transaction.GetHash();

	public bool IsOwnCoinjoin() => Transaction.IsOwnCoinjoin();
}
