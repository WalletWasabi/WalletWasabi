using ReactiveUI;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Buy.Workflows;

public abstract partial class InputValidator : ReactiveObject
{
	[AutoNotify] private string? _message;
	[AutoNotify] private string? _watermark;
	[AutoNotify] private string? _content;

	protected InputValidator(
		IWorkflowValidator workflowValidator,
		string? message,
		string? watermark,
		string? content)
	{
		_message = message;
		_watermark = watermark;
		_content = content;
		WorkflowValidator = workflowValidator;
	}

	protected IWorkflowValidator WorkflowValidator { get; }

	public abstract bool IsValid();

	public abstract string? GetFinalMessage();

	public virtual bool CanDisplayMessage()
	{
		return true;
	}

	public virtual void OnActivation()
	{
	}

	public virtual bool OnCompletion()
	{
		return true;
	}
}
