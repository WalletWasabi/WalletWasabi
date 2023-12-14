using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.BuyAnything;
using WalletWasabi.Wallets;
using WalletWasabi.WebClients.BuyAnything;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Buy.Workflows;

public class StartConversationStep : WorkflowStep2<ConversationId>
{
	private readonly Wallet _wallet;

	public StartConversationStep(Conversation2 conversation, Wallet wallet) : base(conversation)
	{
		_wallet = wallet;
	}

	public override async Task<Conversation2> ExecuteAsync(Conversation2 conversation)
	{
		if (conversation.Id != ConversationId.Empty)
		{
			return conversation;
		}

		conversation = await StartNewConversationAsync(conversation);

		return conversation;
	}

	protected override Conversation2 PutValue(Conversation2 conversation, ConversationId value) => conversation with { Id = value };

	protected override ConversationId? RetrieveValue(Conversation2 conversation) => conversation.Id;

	private async Task<Conversation2> StartNewConversationAsync(Conversation2 conversation)
	{
		if (Services.HostedServices.GetOrDefault<BuyAnythingManager2>() is not { } buyAnythingManager)
		{
			return conversation;
		}

		conversation = await buyAnythingManager.StartNewConversationAsync(_wallet, conversation, CancellationToken.None);

		var hourRange = conversation.MetaData.Product switch
		{
			BuyAnythingClient.Product.ConciergeRequest => "24-48 hours",
			BuyAnythingClient.Product.FastTravelBooking => "24-48 hours",
			BuyAnythingClient.Product.TravelConcierge => "48-72 hours",
			_ => "a few days"
		};

		conversation = AddBotMessage(conversation, $"Thank you! We've received your request and will get in touch with you within {hourRange} (Monday to Friday).");

		return conversation;
	}
}
