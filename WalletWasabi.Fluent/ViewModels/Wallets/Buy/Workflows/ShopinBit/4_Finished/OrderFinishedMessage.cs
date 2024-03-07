using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.BuyAnything;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Buy.Workflows;

public class OrderFinishedMessage : WorkflowStep<object>
{
	private readonly CancellationToken _token;

	public OrderFinishedMessage(Conversation conversation, CancellationToken token, bool isEditing = false) : base(conversation, token, isEditing)
	{
		_token = token;

		SetCompleted();
	}

	public override bool IsInteractive => false;

	public override async Task ExecuteAsync()
	{
		await base.ExecuteAsync();

		IsBusy = true;

		try
		{
			await new SaveConversationStep(Conversation, _token).ExecuteAsync();
		}
		finally
		{
			IsBusy = false;
		}
	}

	protected override IEnumerable<string> BotMessages(Conversation conversation)
	{
		yield return "I'll be available for the next 30 days to assist with any questions you might have. Thank you very much for using the Buy Anything Button!";
	}

	protected override object? RetrieveValue(Conversation conversation) => null;

	protected override Conversation PutValue(Conversation conversation, object value) => conversation;
}
