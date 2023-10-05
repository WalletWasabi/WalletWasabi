using NBitcoin;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Models;

namespace WalletWasabi.Blockchain.Transactions;

public class TransactionSummary
{
	private Money _amount;

	public TransactionSummary(SmartTransaction tx, Money amount)
	{
		Transaction = tx;
		_amount = amount;
	}

	public SmartTransaction Transaction { get; }
	public Money Amount
	{
		get
		{
			lock (Lock)
			{
				return _amount;
			}
		}

		set
		{
			lock (Lock)
			{
				_amount = value;
			}
		}
	}

	private object Lock { get; } = new();
	public DateTimeOffset FirstSeen => Transaction.FirstSeen;
    public LabelsArray Labels => Transaction.Labels;
	public Height Height => Transaction.Height;
	public uint256? BlockHash => Transaction.BlockHash;
	public int BlockIndex => Transaction.BlockIndex;
	public bool IsCancellation => Transaction.IsCancellation;
	public bool IsSpeedup => Transaction.IsSpeedup;
	public bool IsCPFP => Transaction.IsCPFP;
	public bool IsCPFPd => Transaction.IsCPFPd;

	public Money? GetFee() => Transaction.GetFee();
	public uint256 GetHash() => Transaction.GetHash();
	public bool IsOwnCoinjoin() => Transaction.IsOwnCoinjoin();
}
