using System.Threading.Tasks;
using ReactiveUI;
using WalletWasabi.BuyAnything;
using WalletWasabi.Fluent.ViewModels.Wallets.Buy.Workflows.ShopinBit;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Buy.Workflows;

public abstract partial class Workflow : ReactiveObject
{
	[AutoNotify] private IWorkflowStep? _currentStep;
	[AutoNotify] private Conversation _conversation;

	protected Workflow(Conversation conversation)
	{
		_conversation = conversation;
	}

	public abstract Task ExecuteAsync();

	public abstract IMessageEditor MessageEditor { get; }

	protected async Task ExecuteStepAsync(IWorkflowStep step)
	{
		CurrentStep = step;
		Conversation = await step.ExecuteAsync(Conversation);
	}

	public static Workflow Create(Wallet wallet, Conversation conversation)
	{
		// If another type of workflow is required in the future this is the place where it should be defined
		var workflow = new ShopinBitWorkflow(wallet, conversation);

		return workflow;
	}
}
