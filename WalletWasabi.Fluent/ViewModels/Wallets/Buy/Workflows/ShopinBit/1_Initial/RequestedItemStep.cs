using System.Collections.Generic;
using System.Reactive.Linq;
using System.Threading;
using ReactiveUI;
using WalletWasabi.BuyAnything;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Buy.Workflows;

/// <summary>
/// ShopinBit Step #3: Input your request.
/// </summary>
public class RequestedItemStep : WorkflowStep<string>
{
	public RequestedItemStep(Conversation conversation, CancellationToken token) : base(conversation, token)
	{
		Watermark = "Describe here...";

		this.WhenAnyValue(x => x.Value)
			.Select(text => text?.Length >= MinCharLimit)
			.BindTo(this, x => x.IsInputLengthValid);
	}

	public override int MinCharLimit => 60;

	protected override IEnumerable<string> BotMessages(Conversation conversation)
	{
		// Ask for item to buy
		yield return $"What do you exactly need? Describe it with at least {MinCharLimit} characters and be as precise as possible for a faster response.";
	}

	protected override Conversation PutValue(Conversation conversation, string value) =>
		conversation.UpdateMetadata(m => m with { RequestedItem = value });

	protected override string? RetrieveValue(Conversation conversation) => conversation.MetaData.RequestedItem;

	// This Step is only valid if the Value is actual text
	protected override bool ValidateUserValue(string? value) => !string.IsNullOrWhiteSpace(value?.Trim());

	protected override string StringValue(string? value) => value ?? "";
}
