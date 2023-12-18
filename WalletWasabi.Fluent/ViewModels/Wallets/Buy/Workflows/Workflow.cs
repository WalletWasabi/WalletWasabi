using System.Reactive.Linq;
using System.Threading;
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
	[AutoNotify] private bool _isCompleted;
	[AutoNotify] private bool _isDeletedInSib;

	protected Workflow(Conversation conversation)
	{
		_conversation = conversation;

		BindCurrentStepConversation();

		this.WhenAnyValue(x => x.Conversation)
			.Do(x =>
			{
				if (CurrentStep is { } step)
				{
					step.Conversation = x;
				}
			})
			.Subscribe();
	}

	public abstract Task ExecuteAsync(CancellationToken token);

	public abstract IMessageEditor MessageEditor { get; }

	protected async Task ExecuteStepAsync(IWorkflowStep step)
	{
		CurrentStep = step;
		step.Conversation = Conversation;
		await step.ExecuteAsync();
	}

	protected void WorkflowCompleted()
	{
		CurrentStep = null;
		IsDeletedInSib = true;
	}

	public void Reset()
	{
		// TODO: abort workflow execution using CancellationToken
		CurrentStep?.Ignore();
	}

	/// <summary>
	/// Marks the conversation messages as read and Saves to disk.
	/// </summary>
	public async Task MarkConversationAsReadAsync()
	{
		if (CurrentStep is { })
		{
			CurrentStep.IsBusy = true;
		}

		try
		{
			// TODO: pass cancellationtoken
			var cancellationToken = CancellationToken.None;

			Conversation = Conversation.MarkAsRead();

			if (Conversation.Id == ConversationId.Empty)
			{
				return;
			}

			var buyAnythingManager = Services.HostedServices.Get<BuyAnythingManager>();

			await Task.Run(() => buyAnythingManager.UpdateConversationOnlyLocallyAsync(Conversation, cancellationToken));
		}
		finally
		{
			if (CurrentStep is { })
			{
				CurrentStep.IsBusy = false;
			}
		}
	}

	public static Workflow Create(Wallet wallet, Conversation conversation)
	{
		// If another type of workflow is required in the future this is the place where it should be defined
		var workflow = new ShopinBitWorkflow(wallet, conversation);

		return workflow;
	}

	private void BindCurrentStepConversation()
	{
#pragma warning disable CS8602
		// Dereference of a possibly null reference.
		// Reason: this warning is dubious here.
		// The parameter of WhenAnyValue() is an Expression (from System.Linq.Expressions).
		// It's not directly executable code and therefore it cannot raise a NullReferenceException
		// Also, null propagation isn't allowed by the compiler inside such an Expression,
		// so the only way to remove this warning is to make CurrentStep non-nullable, which doesn't make sense by design.
		this.WhenAnyValue(x => x.CurrentStep.Conversation)
			.BindTo(this, x => x.Conversation);
#pragma warning restore CS8602 // Dereference of a possibly null reference.
	}
}
