using NBitcoin;
using ReactiveUI;
using System.Collections.Generic;
using WalletWasabi.Blockchain.Analysis.Clustering;

namespace WalletWasabi.Fluent.Models.Wallets;

public partial class TransactionModel : ReactiveObject
{
	private readonly List<TransactionModel> _children = new();

	public required int OrderIndex { get; init; }

	public required uint256 Id { get; init; }

	public required LabelsArray Labels { get; init; }

	public required DateTimeOffset Date { get; set; }

	public required string DateString { get; set; }

	public required int Confirmations { get; init; }

	public int BlockHeight { get; init; }

	public uint256? BlockHash { get; init; }

	public required string ConfirmedTooltip { get; set; }

	public required TransactionType Type { get; init; }

	public required TransactionStatus Status { get; set; }

	public bool IsChild { get; set; }

	public Money? Balance { get; set; }

	public required Money Amount { get; set; }

	public Money? IncomingAmount => GetAmounts().IncomingAmount;

	public Money? OutgoingAmount => GetAmounts().OutgoingAmount;

	public Money? Fee { get; set; }

	public bool CanCancelTransaction { get; init; }

	public bool CanSpeedUpTransaction { get; init; }

	public IReadOnlyList<TransactionModel> Children => _children;

	public bool IsConfirmed => Status == TransactionStatus.Confirmed;

	public bool IsCoinjoin => Type is TransactionType.Coinjoin or TransactionType.CoinjoinGroup;

	public bool IsCoinjoinGroup => Type == TransactionType.CoinjoinGroup;

	public bool IsCancellation => Type == TransactionType.Cancellation;

	public FeeRate? FeeRate { get; set; }

	private (Money? IncomingAmount, Money? OutgoingAmount) GetAmounts()
	{
		Money? incomingAmount = null;
		Money? outgoingAmount = null;

		if (Amount < Money.Zero)
		{
			outgoingAmount = -Amount - (Fee ?? Money.Zero);
		}
		else
		{
			incomingAmount = Amount;
		}

		return (incomingAmount, outgoingAmount);
	}

	public void Add(TransactionModel child)
	{
		_children.Add(child);
	}

	public override string ToString()
	{
		return $"{Type} {Status} {DateString} {Amount} {Balance}";
	}
}
