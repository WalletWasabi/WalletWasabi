using ReactiveUI;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Buy.Workflows;

public partial class WorkflowValidatorViewModel : ReactiveObject, IWorkflowValidator
{
	[AutoNotify(SetterModifier = AccessModifier.Private)] private bool _isValid;

	public WorkflowValidatorViewModel()
	{
		IsValidObservable = this.WhenAnyValue(x => x.IsValid);
	}

	public IObservable<bool> IsValidObservable { get; }

	public void Signal(bool isValid)
	{
		IsValid = isValid;
	}
}
