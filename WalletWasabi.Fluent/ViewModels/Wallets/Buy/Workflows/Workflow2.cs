using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ReactiveUI;
using WalletWasabi.BuyAnything;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Buy.Workflows;

public abstract partial class Workflow2 : ReactiveObject
{
	private readonly object _lock = new();

	[AutoNotify] private IWorkflowStep2? _currentStep;
	[AutoNotify(SetterModifier = AccessModifier.Private)] private Conversation2 _conversation;

	protected Workflow2(Conversation2 conversation)
	{
		_conversation = conversation;
	}

	public abstract Task<Conversation2> ExecuteAsync();

	protected async Task ExecuteStepAsync(IWorkflowStep2 step)
	{
		CurrentStep = step;
		var conversation = await step.ExecuteAsync(Conversation);
		SetConversation(conversation);
	}

	protected void SetConversation(Conversation2 conversation)
	{
		lock (_lock)
		{
			Conversation = conversation;
		}
	}
}
