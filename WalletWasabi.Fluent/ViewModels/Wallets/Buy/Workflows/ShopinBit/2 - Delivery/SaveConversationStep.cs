using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.BuyAnything;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Buy.Workflows;

public class SaveConversationStep : WorkflowStep<object>
{
	public SaveConversationStep(Conversation conversation) : base(conversation)
	{
	}

	public override async Task ExecuteAsync()
	{
		if (Conversation.Id == ConversationId.Empty)
		{
			return;
		}

		IsBusy = true;

		var buyAnythingManager = Services.HostedServices.Get<BuyAnythingManager>();

		// TODO: pass cancellationToken
		var cancellationToken = CancellationToken.None;

		await buyAnythingManager.UpdateConversationAsync(Conversation, cancellationToken);

		IsBusy = false;
	}

	protected override Conversation PutValue(Conversation conversation, object value) => conversation;

	protected override object? RetrieveValue(Conversation conversation) => conversation;
}
