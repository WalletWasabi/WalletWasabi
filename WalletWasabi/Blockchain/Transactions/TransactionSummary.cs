using System.Collections.Generic;
using System.Linq;
using NBitcoin;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Blockchain.TransactionProcessing;
using WalletWasabi.Models;

namespace WalletWasabi.Blockchain.Transactions;

/// <summary>
/// A wallet's view of a transaction, copied out of the <see cref="SmartTransaction"/> inside
/// <see cref="TransactionProcessor.WhenIdle"/>, so it never changes while it is being read.
/// </summary>
public class TransactionSummary
{
	public TransactionSummary(SmartTransaction tx, Money amount, FeeRate? effectiveFeeRate, KeyManager keyManager)
	{
		Transaction = tx.Transaction;
		Amount = amount;
		WalletInputs = tx.WalletInputs.ToArray();
		WalletOutputs = tx.WalletOutputs.ToArray();
		FirstSeen = tx.FirstSeen;
		Labels = tx.Labels;
		Height = tx.Height;
		BlockHash = tx.BlockHash;
		BlockIndex = tx.BlockIndex;
		IsCancellation = tx.IsCancellation;
		IsSpeedup = tx.IsSpeedup;
		IsCPFP = tx.IsCPFP;
		CpfpChildren = tx.ChildrenPayForThisTx.Select(x => x.GetHash()).ToArray();
		IsOwnCoinjoin = tx.IsOwnCoinjoin();
		Fee = tx.GetFee();
		FeeRate = tx.TryGetFeeRate(out var feeRate) ? feeRate : effectiveFeeRate;
		UnconfirmedChainFeeRate = tx.Confirmed ? null : tx.GetUnconfirmedChainFeeRate();
		CanCancel = tx.IsCancellable(keyManager);
		CanSpeedUp = tx.IsSpeedupable(keyManager);
	}

	public Transaction Transaction { get; }
	public Money Amount { get; set; }
	public Func<string> Hex => () => Transaction.ToHex();
	public IReadOnlyCollection<SmartCoin> WalletInputs { get; }
	public IReadOnlyCollection<SmartCoin> WalletOutputs { get; }

	public Func<IReadOnlyCollection<OutPoint>> ForeignInputs => () =>
	{
		var walletInputs = WalletInputs.Select(x => x.Outpoint).ToHashSet();
		return Transaction.Inputs.Select(x => x.PrevOut).Where(x => !walletInputs.Contains(x)).ToArray();
	};

	public Func<IReadOnlyCollection<IndexedTxOut>> ForeignOutputs => () =>
	{
		var walletOutputs = WalletOutputs.Select(x => x.Index).ToHashSet();
		return Transaction.Outputs.AsIndexedOutputs().Where(x => !walletOutputs.Contains(x.N)).ToArray();
	};

	public DateTimeOffset FirstSeen { get; }
	public LabelsArray Labels { get; }
	public Height Height { get; }
	public uint256? BlockHash { get; }
	public int BlockIndex { get; }
	public bool IsCancellation { get; }
	public bool IsSpeedup { get; }
	public bool IsCPFP { get; }
	public bool IsCPFPd => CpfpChildren.Count != 0;

	/// <summary>Transactions that pay for this one with CPFP.</summary>
	public IReadOnlyCollection<uint256> CpfpChildren { get; }

	public bool IsOwnCoinjoin { get; }
	public Money? Fee { get; }
	public FeeRate? FeeRate { get; }

	/// <summary>Fee rate of this transaction together with the unconfirmed ones it confirms with, or <c>null</c> when confirmed or unknown.</summary>
	public FeeRate? UnconfirmedChainFeeRate { get; }

	public bool CanCancel { get; }
	public bool CanSpeedUp { get; }

	public uint256 GetHash() => Transaction.GetHash();
}
