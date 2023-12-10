using System.Collections.Generic;
using System.Reactive.Linq;
using System.Threading;
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
		EditStepCommand = ReactiveCommand.Create<WorkflowStep>(TryToEditStep);
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

	public WorkflowStep? TryToGetNextStep(CancellationToken cancellationToken)
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

		for (var i = _nextStepIndex; i < _steps.Count; i++)
		{
			if (cancellationToken.IsCancellationRequested)
			{
				CurrentStep = null;
				return null;
			}

			var result = true;
			var step = _steps[_nextStepIndex];

			if (step.SkipStep())
			{
				if (_nextStepIndex + 1 >= _steps.Count)
				{
					IsCompleted = true;
					CurrentStep = step;
					return step;
				}

				_nextStepIndex++;
				continue;
			}

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

		return null;
	}

	public virtual void TryToEditStep(WorkflowStep step)
	{
		// TODO:
	}

	public abstract WorkflowRequest GetResult();

	protected virtual void CreateCanEditObservable()
	{
		CanEditObservable = Observable.Return(false);
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
