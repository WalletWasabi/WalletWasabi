using System.Reactive;
using System.Reactive.Subjects;
using ReactiveUI;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Buy.Workflows;

public partial class WorkflowValidator : ReactiveObject, IWorkflowValidator
{
	private readonly BehaviorSubject<Unit> _nextStepSubject;

	[AutoNotify(SetterModifier = AccessModifier.Private)] private bool _isValid;

	public WorkflowValidator()
	{
		_nextStepSubject = new BehaviorSubject<Unit>(Unit.Default);

		NextStepObservable = _nextStepSubject;

		IsValidObservable = this.WhenAnyValue(x => x.IsValid);
	}

	public IObservable<Unit> NextStepObservable { get; }

	public IObservable<bool> IsValidObservable { get; }

	public void Signal(bool isValid)
	{
		IsValid = isValid;
	}

	public void NextStep()
	{
		_nextStepSubject.OnNext(Unit.Default);
	}
}
