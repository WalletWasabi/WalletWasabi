using NBitcoin;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Models;

namespace WalletWasabi.Blockchain.Transactions;

public class TransactionSummary
{
	public TransactionSummary(SmartTransaction tx, Money amount, FeeRate? effectiveFeeRate, Money? fee)
	{
		Transaction = tx;
		Amount = amount;
		EffectiveFeeRate = effectiveFeeRate;
		Fee = fee;
	}

	public SmartTransaction Transaction { get; }
	public Money Amount { get; set; }

	// Only available, if we needed the backend's help to calculate tx's fee rate. Use FeeRate().
	public FeeRate? EffectiveFeeRate { get; }

	// Only available, if we needed the backend's help to calculate tx fee. Use GetFee().
	public Money? Fee { get; }

	public DateTimeOffset FirstSeen => Transaction.FirstSeen;
	public LabelsArray Labels => Transaction.Labels;
	public Height Height => Transaction.Height;
	public uint256? BlockHash => Transaction.BlockHash;
	public int BlockIndex => Transaction.BlockIndex;
	public bool IsCancellation => Transaction.IsCancellation;
	public bool IsSpeedup => Transaction.IsSpeedup;
	public bool IsCPFP => Transaction.IsCPFP;
	public bool IsCPFPd => Transaction.IsCPFPd;

	public Money? GetFee() => Transaction.GetFee() ?? Fee;

	public FeeRate? FeeRate() => Transaction.TryGetFeeRate(out var feeRate) ? feeRate : EffectiveFeeRate;

	public uint256 GetHash() => Transaction.GetHash();

	public bool IsOwnCoinjoin() => Transaction.IsOwnCoinjoin();
}
