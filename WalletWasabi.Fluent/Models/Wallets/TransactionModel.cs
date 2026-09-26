using NBitcoin;
using ReactiveUI;
using WalletWasabi.Blockchain.Analysis.Clustering;

namespace WalletWasabi.Fluent.Models.Wallets;

public abstract partial class TransactionModel : ReactiveObject
{
	public required int OrderIndex { get; init; }

	public required uint256 Id { get; init; }

	public required LabelsArray Labels { get; init; }

	public required DateTimeOffset Date { get; init; }

	public required string DateString { get; set; }

	public required string DateToolTipString { get; init; }

	public required string ConfirmedTooltip { get; init; }

	public abstract TransactionType Type { get; }

	public required TransactionStatus Status { get; init; }

	public bool IsChild { get; set; }

	public required Money Amount { get; init; }

	public Amount AmountAmount => new (Amount);

	public bool IsConfirmed => Status == TransactionStatus.Confirmed;

	public override string ToString()
	{
		return $"{Type} {Status} {DateString} {Amount}";
	}
}
