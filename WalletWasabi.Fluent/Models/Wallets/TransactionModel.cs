using NBitcoin;
using ReactiveUI;
using System.Collections.Generic;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.TransactionOutputs;

namespace WalletWasabi.Fluent.Models.Wallets;

public partial class TransactionModel : ReactiveObject
{
	private readonly List<TransactionModel> _children = new();

	public required int OrderIndex { get; init; }

	public required uint256 Id { get; init; }

	public required LabelsArray Labels { get; init; }

	public required DateTimeOffset Date { get; set; }

	public required string DateString { get; set; }

	public required string DateToolTipString { get; set; }

	public required uint Confirmations { get; init; }

	public uint BlockHeight { get; init; }

	public uint256? BlockHash { get; init; }

	public required string ConfirmedTooltip { get; set; }

	public required TransactionType Type { get; init; }

	public required TransactionStatus Status { get; set; }

	public bool IsChild { get; set; }

	public required Func<string> HexFunction { get; set; }
	public Lazy<string> Hex => new(HexFunction());

	public required Func<IReadOnlyCollection<OutPoint>> ForeignInputsFunction{ get; set; }
	public Lazy<IReadOnlyCollection<OutPoint>> ForeignInputs => new(ForeignInputsFunction());
	public required IReadOnlyCollection<SmartCoin> WalletInputs { get; set; }

	public required Func<IReadOnlyCollection<IndexedTxOut>> ForeignOutputsFunction{ get; set; }
	public Lazy<IReadOnlyCollection<IndexedTxOut>> ForeignOutputs => new(ForeignOutputsFunction());
	public required IReadOnlyCollection<SmartCoin> WalletOutputs { get; set; }
	public required Money Amount { get; set; }

	public Amount AmountAmount => new (Amount);

	public Money? Fee { get; set; }

	public bool CanCancelTransaction { get; init; }

	public bool CanSpeedUpTransaction { get; init; }

	public IReadOnlyList<TransactionModel> Children => _children;

	public bool IsConfirmed => Status == TransactionStatus.Confirmed;

	public bool IsCoinjoin => Type is TransactionType.Coinjoin or TransactionType.CoinjoinGroup;

	public bool IsCoinjoinGroup => Type == TransactionType.CoinjoinGroup;

	public bool IsCancellation => Type == TransactionType.Cancellation;

	public FeeRate? FeeRate { get; set; }

	public bool HasBeenSpedUp { get; set; }

	public void Add(TransactionModel child)
	{
		_children.Add(child);
	}

	public override string ToString()
	{
		return $"{Type} {Status} {DateString} {Amount}";
	}
}
