using ReactiveUI;
using WalletWasabi.BuyAnything;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Buy.Workflows;

public abstract partial class UserInput2 : ReactiveObject
{
	public virtual void OnActivation()
	{
	}

	public abstract string ToMessage();
}

public abstract partial class InputValidator : ReactiveObject
{
	private readonly Func<string?>? _messageProvider;
	[AutoNotify] private string? _message;
	[AutoNotify] private string? _watermark;
	[AutoNotify] private string? _content;

	protected InputValidator(
		WorkflowState workflowState,
		Func<string?>? messageProvider,
		string? watermark,
		string? content,
		ChatMessageMetaData.ChatMessageTag tag = ChatMessageMetaData.ChatMessageTag.None)
	{
		_messageProvider = messageProvider;
		_watermark = watermark;
		_content = content;
		WorkflowState = workflowState;
		Tag = tag;
	}

	protected WorkflowState WorkflowState { get; }

	public virtual ChatMessageMetaData.ChatMessageTag Tag { get; }

	public abstract bool IsValid();

	public abstract string? GetFinalMessage();

	public virtual bool CanDisplayMessage()
	{
		return true;
	}

	public virtual void OnActivation()
	{
		WorkflowState.SignalValid(false);

		if (_messageProvider != null)
		{
			Message = _messageProvider.Invoke();
		}
	}

	public virtual bool OnCompletion()
	{
		return true;
	}
}
