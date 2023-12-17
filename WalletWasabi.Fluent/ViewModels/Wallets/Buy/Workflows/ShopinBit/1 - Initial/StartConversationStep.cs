using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.BuyAnything;
using WalletWasabi.Wallets;
using WalletWasabi.WebClients.BuyAnything;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Buy.Workflows;

public class StartConversationStep : WorkflowStep<ConversationId>
{
	private readonly Wallet _wallet;

	public StartConversationStep(Conversation conversation, Wallet wallet) : base(conversation)
	{
		_wallet = wallet;
	}

	public override async Task ExecuteAsync()
	{
		if (Conversation.Id != ConversationId.Empty)
		{
			return;
		}

		IsBusy = true;

		Conversation = await StartNewConversationAsync(Conversation);

		IsBusy = false;
	}

	protected override Conversation PutValue(Conversation conversation, ConversationId value) => conversation with { Id = value };

	protected override ConversationId? RetrieveValue(Conversation conversation) => conversation.Id;

	private async Task<Conversation> StartNewConversationAsync(Conversation conversation)
	{
		var buyAnythingManager = Services.HostedServices.Get<BuyAnythingManager>();

		conversation = await buyAnythingManager.StartNewConversationAsync(_wallet, conversation, CancellationToken.None);

		var hourRange = conversation.MetaData.Product switch
		{
			BuyAnythingClient.Product.ConciergeRequest => "24-48 hours",
			BuyAnythingClient.Product.FastTravelBooking => "24-48 hours",
			BuyAnythingClient.Product.TravelConcierge => "48-72 hours",
			_ => "a few days"
		};

		conversation = conversation.AddBotMessage($"Thank you! We've received your request and will get in touch with you within {hourRange} (Monday to Friday).");

		return conversation;
	}
}
