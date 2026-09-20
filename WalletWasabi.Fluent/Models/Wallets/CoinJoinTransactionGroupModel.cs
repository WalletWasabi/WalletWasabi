using System.Collections.Generic;
using WalletWasabi.WabiSabi.Client;

namespace WalletWasabi.Fluent.Models.Wallets;

public class CoinJoinTransactionGroupModel : TransactionModel
{
	public override TransactionType Type => TransactionType.CoinjoinGroup;

	public required IReadOnlyList<CoinJoinTransactionModel> Children { get; init; }

	public CoinjoinCosts? CoinjoinCosts { get; init; }
}
