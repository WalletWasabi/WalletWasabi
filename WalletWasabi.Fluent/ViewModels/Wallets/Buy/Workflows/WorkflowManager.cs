using System.Threading;
using ReactiveUI;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Buy.Workflows;

public abstract partial class WorkflowManager : ReactiveObject
{
	[AutoNotify(SetterModifier = AccessModifier.Private)]
	private Workflow? _currentWorkflow;

	public IWorkflowValidator WorkflowValidator { get; } = new WorkflowValidator();

	public void ResetWorkflow()
	{
		if (_currentWorkflow?.CanCancel() ?? true)
		{
			CurrentWorkflow = null;
		}
	}

	public void InvokeOutputs(Action<string> onAssistantMessage, CancellationToken cancellationToken)
	{
		if (CurrentWorkflow is null)
		{
			return;
		}

		while (true)
		{
			// TODO:
			// if (cancellationToken.IsCancellationRequested)
			// {
			// 	return;
			// }

			var peekStep = CurrentWorkflow.PeekNextStep();
			if (peekStep is null)
			{
				break;
			}

			var nextStep = CurrentWorkflow.TryToGetNextStep(cancellationToken);
			if (nextStep is null)
			{
				continue;
			}

			if (nextStep.UserInputValidator.CanDisplayMessage())
			{
				var message = nextStep.UserInputValidator.GetFinalMessage();
				if (message is not null)
				{
					onAssistantMessage(message);
				}
			}

			if (nextStep.RequiresUserInput)
			{
				break;
			}

			if (!nextStep.UserInputValidator.OnCompletion())
			{
				break;
			}
		}
	}

	public bool InvokeInputs(Action<string> onUserMessage, Action<string> onAssistantMessage, object? args, CancellationToken cancellationToken)
	{
		WorkflowValidator.SignalValid(false);

		if (CurrentWorkflow is null)
		{
			return false;
		}

		if (CurrentWorkflow.CurrentStep is not null)
		{
			if (!CurrentWorkflow.CurrentStep.UserInputValidator.OnCompletion())
			{
				return false;
			}

			if (CurrentWorkflow.CurrentStep.UserInputValidator.CanDisplayMessage())
			{
				var message = CurrentWorkflow.CurrentStep.UserInputValidator.GetFinalMessage();

				if (message is not null)
				{
					onUserMessage(message);
				}
			}
		}

		if (CurrentWorkflow.IsCompleted)
		{
			OnSelectNextWorkflow(null, args, onAssistantMessage, cancellationToken);
			return false;
		}

		var nextStep = CurrentWorkflow.TryToGetNextStep(cancellationToken);
		if (nextStep is null)
		{
			return false;
		}

		if (!nextStep.UserInputValidator.OnCompletion())
		{
			return false;
		}

		if (!nextStep.RequiresUserInput)
		{
			if (nextStep.UserInputValidator.CanDisplayMessage())
			{
				var nextMessage = nextStep.UserInputValidator.GetFinalMessage();
				if (nextMessage is not null)
				{
					onAssistantMessage(nextMessage);
				}
			}
		}

		if (nextStep.IsCompleted)
		{
			InvokeOutputs(onAssistantMessage, cancellationToken);
		}

		return true;
	}

	public abstract bool OnSelectNextWorkflow(
		string? context,
		object? args,
		Action<string> onAssistantMessage,
		CancellationToken cancellationToken);
}
