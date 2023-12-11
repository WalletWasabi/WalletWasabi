using ReactiveUI;
using WalletWasabi.Fluent.ViewModels.HelpAndSupport;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Buy.Workflows.ShopinBit;

public partial class ConfirmTosInputValidator : InputValidator
{
	[AutoNotify] private bool _hasAcceptedTermsOfService;
	[AutoNotify] private LinkViewModel _link;

	public ConfirmTosInputValidator(
		WorkflowState workflowState,
		LinkViewModel link,
		Func<string?> message,
		string content)
		: base(workflowState, message, null, content)
	{
		_link = link;

		this.WhenAnyValue(x => x.HasAcceptedTermsOfService)
			.Subscribe(_ => WorkflowState.SignalValid(IsValid()));
	}

	public override bool IsValid()
	{
		return HasAcceptedTermsOfService;
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
