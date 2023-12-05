using System.Collections.Generic;
using WalletWasabi.BuyAnything;
using WalletWasabi.Fluent.ViewModels.Wallets.Buy.Messages;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Buy;

public record OrderUpdateMessage(
	ConversationId Id,
	string? ConversationStatus,
	string? OrderStatus,
	IReadOnlyList<MessageViewModel>? Messages)
{
	public static readonly OrderUpdateMessage Empty = new (ConversationId.Empty, null, null, null);
}
