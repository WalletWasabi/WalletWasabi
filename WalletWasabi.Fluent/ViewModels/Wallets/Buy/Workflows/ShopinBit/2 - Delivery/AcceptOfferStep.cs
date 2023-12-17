using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.BuyAnything;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Buy.Workflows;

public class AcceptOfferStep : WorkflowStep<object>
{
	public AcceptOfferStep(Conversation conversation) : base(conversation)
	{
	}

	public override async Task ExecuteAsync()
	{
		if (Conversation.MetaData.OfferAccepted)
		{
			return;
		}

		IsBusy = true;

		var buyAnythingManager = Services.HostedServices.Get<BuyAnythingManager>();

		// TODO: pass cancellationtoken
		var cancellationToken = CancellationToken.None;

		var newConversation = Conversation.UpdateMetadata(m => m with { OfferAccepted = true });
		newConversation = await buyAnythingManager.AcceptOfferAsync(newConversation, cancellationToken);

		Conversation = newConversation;

		IsBusy = false;
	}

	protected override Conversation PutValue(Conversation conversation, object value) => conversation;

	protected override object? RetrieveValue(Conversation conversation) => conversation;
}
