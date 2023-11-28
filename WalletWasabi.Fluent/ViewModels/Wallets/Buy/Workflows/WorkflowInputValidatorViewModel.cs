using ReactiveUI;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Buy.Workflows;

public abstract partial class WorkflowInputValidatorViewModel : ReactiveObject
{
	[AutoNotify] private string? _message;

	protected WorkflowInputValidatorViewModel(string? message)
	{
		_message = message;
	}

	public abstract bool IsValid();

	public abstract string? GetFinalMessage();
}
