using System.Reactive;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Buy.Workflows;

public interface IWorkflowValidator
{
	IObservable<Unit> NextStepObservable { get; }

	IObservable<bool> IsValidObservable { get; }

	void SignalValid(bool isValid);

	void SignalNextStep();
}
