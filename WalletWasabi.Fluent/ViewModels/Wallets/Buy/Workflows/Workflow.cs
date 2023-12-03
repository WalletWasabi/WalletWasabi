using System.Collections.Generic;
using System.Reactive.Linq;
using System.Windows.Input;
using ReactiveUI;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Buy.Workflows;

public abstract partial class Workflow : ReactiveObject
{
	[AutoNotify] private List<WorkflowStep>? _steps;
	[AutoNotify(SetterModifier = AccessModifier.Private)] private WorkflowStep? _currentStep;
	[AutoNotify(SetterModifier = AccessModifier.Private)] private bool _isCompleted;

	private int _nextStepIndex = 0;

	protected Workflow()
	{
		EditStepCommand = ReactiveCommand.Create<WorkflowStep>(EditStep);
	}

	public IObservable<bool>? CanEditObservable { get; protected set; }

	public ICommand EditStepCommand { get; }

	public WorkflowStep? PeekNextStep()
	{
		if (_steps is null)
		{
			return null;
		}

		if (_nextStepIndex >= _steps.Count || IsCompleted)
		{
			return null;
		}

		return _steps[_nextStepIndex];
	}

	public WorkflowStep? ExecuteNextStep()
	{
		if (_steps is null)
		{
			CurrentStep = null;
			return null;
		}

		if (_nextStepIndex >= _steps.Count || IsCompleted)
		{
			CurrentStep = null;
			return null;
		}

		var result = true;
		var step = _steps[_nextStepIndex];

		step.UserInputValidator.OnActivation();

		if (step.RequiresUserInput)
		{
			result = step.UserInputValidator.IsValid();
		}

		if (result)
		{
			step.IsCompleted = true;
		}

		if (result)
		{
			if (_nextStepIndex + 1 >= _steps.Count)
			{
				IsCompleted = true;
			}
			else
			{
				_nextStepIndex++;
			}
		}

		CurrentStep = step;
		return step;
	}

	public void EditStep(WorkflowStep step)
	{
		// TODO:
	}

	public abstract WorkflowRequest GetResult();

	protected virtual void CreateCanEditObservable()
	{
		CanEditObservable = this.WhenAnyValue(x => x.IsCompleted).Select(x => !x);
	}

	/// <summary>
	///
	/// </summary>
	/// <returns>True if workflow can be canceled.</returns>
	public virtual bool CanCancel()
	{
		return true;
	}
}
