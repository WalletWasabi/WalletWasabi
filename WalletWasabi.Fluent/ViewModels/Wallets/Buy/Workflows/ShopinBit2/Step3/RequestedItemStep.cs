using System.Collections.Generic;
using WalletWasabi.BuyAnything;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Buy.Workflows;

/// <summary>
/// ShopinBit Step #3: Input your request.
/// </summary>
public class RequestedItemStep : WorkflowStep2<string>
{
	public RequestedItemStep(Conversation2 conversation) : base(conversation)
	{
		Caption = "Request";
	}

	protected override IEnumerable<string> BotMessages(Conversation2 conversation)
	{
		// Ask for item to buy
		yield return "What specific assistance do you need today? Be as precise as possible for faster response.";
	}

	protected override Conversation2 PutValue(Conversation2 conversation, string value) =>
		conversation.UpdateMetadata(m => m with { RequestedItem = value });

	protected override string? RetrieveValue(Conversation2 conversation) => conversation.MetaData.RequestedItem;

	// This Step is only valid if the Value is actual text
	protected override bool ValidateUserValue(string? value) => !string.IsNullOrWhiteSpace(value?.Trim());

	protected override string StringValue(string? value) => value ?? "";
}
