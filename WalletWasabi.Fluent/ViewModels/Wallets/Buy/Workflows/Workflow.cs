using System.Threading.Tasks;
using ReactiveUI;
using WalletWasabi.BuyAnything;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Buy.Workflows;

public abstract partial class Workflow : ReactiveObject
{
	[AutoNotify] private IWorkflowStep? _currentStep;
	[AutoNotify] private Conversation _conversation;

	protected Workflow(Conversation conversation)
	{
		_conversation = conversation;
	}

	public abstract Task<Conversation> ExecuteAsync();

	public abstract IChatMessageEditor GetChatMessageEditor();

	protected async Task ExecuteStepAsync(IWorkflowStep step)
	{
		CurrentStep = step;
		Conversation = await step.ExecuteAsync(Conversation);
	}
}
