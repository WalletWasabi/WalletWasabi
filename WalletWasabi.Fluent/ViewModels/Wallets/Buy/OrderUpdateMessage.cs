using System.Collections.Generic;
using WalletWasabi.BuyAnything;
using WalletWasabi.Fluent.ViewModels.Wallets.Buy.Messages;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Buy;

public record OrderUpdateMessage(
	ConversationId Id,
	string? Command,
	IReadOnlyList<MessageViewModel>? Messages);
