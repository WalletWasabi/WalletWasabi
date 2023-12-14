using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ReactiveUI;
using WalletWasabi.BuyAnything;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Buy.Workflows;

public abstract partial class Workflow2 : ReactiveObject
{
	[AutoNotify] private IWorkflowStep2? _currentStep;

	public async Task<Conversation2> ExecuteAsync(Conversation2 conversation)
	{
		var steps = GetSteps(conversation).ToArray();

		foreach (var step in steps)
		{
			CurrentStep = step;
			conversation = await step.ExecuteAsync(conversation);
		}

		return conversation;
	}

	public abstract IEnumerable<IWorkflowStep2> GetSteps(Conversation2 conversation);
}
