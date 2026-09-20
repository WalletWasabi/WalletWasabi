using NBitcoin;

namespace WalletWasabi.Fluent.Models.Wallets;

public class RegularTransactionModel : SingleTransactionModel
{
	public RegularTransactionModel(TransactionType type)
	{
		Type = type;
	}

	public override TransactionType Type { get; }

	public uint BlockHeight { get; init; }

	public Money? Fee { get; init; }

	public bool CanCancelTransaction { get; init; }

	public bool CanSpeedUpTransaction { get; init; }

	public bool HasBeenSpedUp { get; init; }
}
