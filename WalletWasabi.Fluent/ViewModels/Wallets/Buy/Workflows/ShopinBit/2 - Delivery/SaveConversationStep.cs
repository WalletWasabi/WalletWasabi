using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.BuyAnything;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Buy.Workflows;

public class SaveConversationStep : WorkflowStep<object>
{
	public SaveConversationStep(Conversation conversation) : base(conversation)
	{
	}

	public override async IAsyncEnumerable<Conversation> ExecuteAsync(Conversation conversation)
	{
		if (conversation.Id == ConversationId.Empty)
		{
			yield break;
		}

		IsBusy = true;

		var buyAnythingManager = Services.HostedServices.Get<BuyAnythingManager>();

		// TODO: pass cancellationToken
		var cancellationToken = CancellationToken.None;

		await buyAnythingManager.UpdateConversationAsync(conversation, cancellationToken);

		yield return conversation;

		IsBusy = false;
	}

	protected override Conversation PutValue(Conversation conversation, object value) => conversation;

	protected override object? RetrieveValue(Conversation conversation) => conversation;
}
