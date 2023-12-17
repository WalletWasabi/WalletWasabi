using System.Collections.Generic;
using System.Threading;
using WalletWasabi.BuyAnything;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Buy.Workflows;

/// <summary>
/// ShopinBit Step #3: Input your request.
/// </summary>
public class RequestedItemStep : WorkflowStep<string>
{
	public RequestedItemStep(Conversation conversation, CancellationToken token) : base(conversation, token)
	{
		Caption = "Request";
	}

	protected override IEnumerable<string> BotMessages(Conversation conversation)
	{
		// Ask for item to buy
		yield return "What specific assistance do you need today? Be as precise as possible for faster response.";
	}

	protected override Conversation PutValue(Conversation conversation, string value) =>
		conversation.UpdateMetadata(m => m with { RequestedItem = value });

	protected override string? RetrieveValue(Conversation conversation) => conversation.MetaData.RequestedItem;

	// This Step is only valid if the Value is actual text
	protected override bool ValidateUserValue(string? value) => !string.IsNullOrWhiteSpace(value?.Trim());

	protected override string StringValue(string? value) => value ?? "";
}
