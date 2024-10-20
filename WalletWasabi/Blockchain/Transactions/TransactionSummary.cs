using System.Collections.Generic;
using System.Linq;
using NBitcoin;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Models;

namespace WalletWasabi.Blockchain.Transactions;

public class TransactionSummary
{
	public TransactionSummary(SmartTransaction tx, Money amount, FeeRate? effectiveFeeRate)
	{
		Transaction = tx;
		Amount = amount;
		EffectiveFeeRate = effectiveFeeRate;
	}

	public SmartTransaction Transaction { get; }
	public Money Amount { get; set; }
	public FeeRate? EffectiveFeeRate { get; }

	public Func<IReadOnlyCollection<OutPoint>> ForeignInputs => () => Transaction.ForeignInputs.Select(x => x.PrevOut).ToArray();
	public IReadOnlyCollection<SmartCoin> WalletInputs => Transaction.WalletInputs;
	public Func<IReadOnlyCollection<IndexedTxOut>> ForeignOutputs => () => Transaction.ForeignOutputs;
	public IReadOnlyCollection<SmartCoin> WalletOutputs => Transaction.WalletOutputs;
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

	public FeeRate? FeeRate() => Transaction.TryGetFeeRate(out var feeRate) ? feeRate : EffectiveFeeRate;

	public uint256 GetHash() => Transaction.GetHash();

	public bool IsOwnCoinjoin() => Transaction.IsOwnCoinjoin();
}
