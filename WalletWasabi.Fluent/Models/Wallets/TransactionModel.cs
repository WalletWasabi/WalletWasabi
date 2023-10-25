using NBitcoin;
using ReactiveUI;
using System.Collections.Generic;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.TransactionBuilding;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Wallets;

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

public partial class TransactionModel : ReactiveObject
{
	private readonly List<TransactionModel> _children = new();

	public int OrderIndex { get; init; }

	public uint256 Id { get; init; }

	public LabelsArray Labels { get; init; }

	public DateTimeOffset Date { get; set; }

	public string DateString { get; set; }

	public int Confirmations { get; init; }

	public string ConfirmedTooltip { get; set; }

	public TransactionType Type { get; init; }

	public TransactionStatus Status { get; set; }

	public TransactionSummary TransactionSummary { get; init; }

	public bool IsChild { get; set; }

	public Money? Balance { get; set; }

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

	public override string ToString()
	{
		return $"{Type} {Status} {DateString} {Amount} {Balance}";
	}
}
