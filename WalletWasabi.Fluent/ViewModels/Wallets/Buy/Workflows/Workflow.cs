using System.Threading.Tasks;
using ReactiveUI;
using WalletWasabi.BuyAnything;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Buy.Workflows;

public abstract partial class Workflow : ReactiveObject
{
	private readonly object _lock = new();

	[AutoNotify] private IWorkflowStep? _currentStep;
	[AutoNotify(SetterModifier = AccessModifier.Private)] private Conversation _conversation;

	protected Workflow(Conversation conversation)
	{
		_conversation = conversation;
	}

	public abstract Task<Conversation> ExecuteAsync();

	public abstract IChatMessageEditor GetChatMessageEditor();

	protected async Task ExecuteStepAsync(IWorkflowStep step)
	{
		CurrentStep = step;
		var conversation = await step.ExecuteAsync(Conversation);
		SetConversation(conversation);
	}

	protected void SetConversation(Conversation conversation)
	{
		lock (_lock)
		{
			Conversation = conversation;
		}
	}
}
