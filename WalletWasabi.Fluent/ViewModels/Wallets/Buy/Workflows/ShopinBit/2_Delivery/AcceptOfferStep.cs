using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.BuyAnything;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Buy.Workflows;

public class AcceptOfferStep : WorkflowStep<object>
{
	private readonly CancellationToken _token;

	public AcceptOfferStep(Conversation conversation, CancellationToken token) : base(conversation, token)
	{
		_token = token;
	}

	public override async Task ExecuteAsync()
	{
		if (Conversation.MetaData.OfferAccepted)
		{
			return;
		}

		try
		{
			IsBusy = true;

			var buyAnythingManager = Services.HostedServices.Get<BuyAnythingManager>();

			var newConversation = Conversation.UpdateMetadata(m => m with { OfferAccepted = true });
			newConversation = await buyAnythingManager.AcceptOfferAsync(newConversation, _token);

			Conversation = newConversation;
		}
		finally
		{
			IsBusy = false;
		}
	}

	protected override Conversation PutValue(Conversation conversation, object value) => conversation;

	protected override object? RetrieveValue(Conversation conversation) => conversation;
}
