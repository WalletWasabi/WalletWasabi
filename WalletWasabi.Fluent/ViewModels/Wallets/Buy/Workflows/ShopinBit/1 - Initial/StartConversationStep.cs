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
	private readonly CancellationToken _token;

	public StartConversationStep(Conversation conversation, Wallet wallet, CancellationToken token) : base(conversation, token)
	{
		_wallet = wallet;
		_token = token;
	}

	public override async Task ExecuteAsync()
	{
		if (Conversation.Id != ConversationId.Empty)
		{
			return;
		}

		try
		{
			IsBusy = true;

			Conversation = await StartNewConversationAsync(Conversation);
		}
		finally
		{
			IsBusy = false;
		}
	}

	protected override Conversation PutValue(Conversation conversation, ConversationId value) => conversation with { Id = value };

	protected override ConversationId? RetrieveValue(Conversation conversation) => conversation.Id;

	private async Task<Conversation> StartNewConversationAsync(Conversation conversation)
	{
		var buyAnythingManager = Services.HostedServices.Get<BuyAnythingManager>();

		conversation = await buyAnythingManager.StartNewConversationAsync(_wallet, conversation, _token);

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
