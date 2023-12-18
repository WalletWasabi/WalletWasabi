using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.BuyAnything;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Buy.Workflows;

public class SaveConversationStep : WorkflowStep<object>
{
	private readonly CancellationToken _token;

	public SaveConversationStep(Conversation conversation, CancellationToken token) : base(conversation, token)
	{
		_token = token;
		_ignored = false;
	}

	public override async Task ExecuteAsync()
	{
		if (Conversation.Id == ConversationId.Empty || _ignored)
		{
			return;
		}

		IsBusy = true;

		try
		{
			var buyAnythingManager = Services.HostedServices.Get<BuyAnythingManager>();

			await Task.Run(() => buyAnythingManager.UpdateConversationAsync(Conversation, _token));
		}
		finally
		{
			IsBusy = false;
		}
	}

	protected override Conversation PutValue(Conversation conversation, object value) => conversation;

	protected override object? RetrieveValue(Conversation conversation) => conversation;
}
