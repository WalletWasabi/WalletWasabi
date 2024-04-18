using NBitcoin;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Models;

namespace WalletWasabi.Blockchain.Transactions;

public class TransactionSummary
{
	public TransactionSummary(SmartTransaction tx, Money amount, FeeRate? feeRate, FeeRate? effectiveFeeRate)
	{
		Transaction = tx;
		Amount = amount;
		FeeRate = feeRate;
		EffectiveFeeRate = effectiveFeeRate;
	}

	public SmartTransaction Transaction { get; }
	public Money Amount { get; set; }
	private FeeRate? EffectiveFeeRate { get; }
	private FeeRate? FeeRate { get; }

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

	public FeeRate? GetFeeRate() => Transaction.TryGetFeeRate(out var feeRate) ? feeRate : FeeRate;

	public FeeRate? GetEffectiveFeeRate() => EffectiveFeeRate;

	public uint256 GetHash() => Transaction.GetHash();

	public bool IsOwnCoinjoin() => Transaction.IsOwnCoinjoin();
}
