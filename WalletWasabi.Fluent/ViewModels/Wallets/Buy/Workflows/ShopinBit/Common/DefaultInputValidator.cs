using System.Reactive.Linq;
using ReactiveUI;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Buy.Workflows.ShopinBit;

public partial class DefaultInputValidator : InputValidator
{
	public DefaultInputValidator(
		WorkflowState workflowState,
		Func<string?> message,
		string? watermark = "Type here...",
		string? content = "Request") : base(workflowState, message, watermark, content)
	{
		this.WhenAnyValue(x => x.Message)
			.Subscribe(_ => WorkflowState.SignalValid(IsValid()));
	}

	public override bool IsValid()
	{
		return !string.IsNullOrWhiteSpace(Message);
	}

	public override string? GetFinalMessage()
	{
		if (IsValid())
		{
			return Message;
		}

		return null;
	}
}
