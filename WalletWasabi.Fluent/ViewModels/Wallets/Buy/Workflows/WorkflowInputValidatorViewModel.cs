using ReactiveUI;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Buy.Workflows;

public abstract partial class WorkflowInputValidatorViewModel : ReactiveObject
{
	[AutoNotify] private string? _message;
	[AutoNotify] private string? _watermark;

	protected WorkflowInputValidatorViewModel(IWorkflowValidator workflowValidator, string? message, string? watermark)
	{
		_message = message;
		_watermark = watermark;
		WorkflowValidator = workflowValidator;
	}

	protected IWorkflowValidator WorkflowValidator { get; }

	public abstract bool IsValid();

	public abstract string? GetFinalMessage();
}
