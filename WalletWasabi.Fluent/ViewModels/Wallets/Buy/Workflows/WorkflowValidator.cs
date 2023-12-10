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
		IsValidObservable = this.WhenAnyValue(x => x.IsValid);
		NextStepObservable = _nextStepSubject;
	}

	public IObservable<bool> IsValidObservable { get; }

	public IObservable<Unit> NextStepObservable { get; }

	public void SignalValid(bool isValid)
	{
		IsValid = isValid;
	}

	public void SignalNextStep()
	{
		_nextStepSubject.OnNext(Unit.Default);
	}
}
