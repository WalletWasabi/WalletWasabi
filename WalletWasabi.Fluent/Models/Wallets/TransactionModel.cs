using NBitcoin;
using ReactiveUI;
using System.Collections.Generic;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.Transactions;

namespace WalletWasabi.Fluent.Models.Wallets;

public enum TransactionType
{
	Unknown,
	IncomingTransaction,
	OutgoingTransaction,
	SelfTransferTransaction,
	Coinjoin,
	CoinjoinGroup,
	Cancellation,
	CPFP
}

public enum TransactionStatus
{
	Unknown,
	Confirmed,
	Pending,
	SpeedUp,
}

public class TransactionModel : ReactiveObject
{
	private List<TransactionModel> _children = new();

	public required int OrderIndex { get; init; }

	public required uint256 Id { get; init; }

	public required LabelsArray Labels { get; init; }

	public required DateTimeOffset Date { get; set; }

	public required string DateString { get; set; }

	public required int Confirmations { get; init; }

	public required string ConfirmedTooltip { get; set; }

	public required TransactionType Type { get; init; }

	public required TransactionStatus Status { get; set; }

	public required TransactionSummary TransactionSummary { get; init; }

	public bool IsChild { get; set; }

	public Money Balance { get; set; }

	public Money? IncomingAmount { get; set; }

	public Money? OutgoingAmount { get; set; }

	public Money? Fee { get; init; }

	public Money Amount => Math.Abs(IncomingAmount ?? OutgoingAmount ?? Money.Zero);

	public bool CanCancelTransaction { get; init; }

	public bool CanSpeedUpTransaction { get; init; }

	public IReadOnlyList<TransactionModel> Children => _children;

	public bool IsConfirmed => Status == TransactionStatus.Confirmed;

	public bool IsCoinjoin => Type is TransactionType.Coinjoin or TransactionType.CoinjoinGroup;

	public bool IsCoinjoinGroup => Type == TransactionType.CoinjoinGroup;

	public bool IsCancellation => Type == TransactionType.Cancellation;

	public void Add(TransactionModel child)
	{
		_children.Add(child);
	}
}
