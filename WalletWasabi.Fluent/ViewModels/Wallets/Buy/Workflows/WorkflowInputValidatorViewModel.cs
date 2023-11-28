using ReactiveUI;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Buy.Workflows;

public abstract partial class WorkflowInputValidatorViewModel : ReactiveObject
{
	[AutoNotify] private string? _message;
	[AutoNotify] private string? _watermark;

	protected WorkflowInputValidatorViewModel(string? message, string? watermark)
	{
		_message = message;
		_watermark = watermark;
	}

	public abstract bool IsValid();

	public abstract string? GetFinalMessage();
}
