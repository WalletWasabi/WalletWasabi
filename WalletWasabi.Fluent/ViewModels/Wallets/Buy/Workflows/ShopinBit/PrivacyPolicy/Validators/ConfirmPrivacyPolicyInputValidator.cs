using ReactiveUI;
using WalletWasabi.Fluent.ViewModels.HelpAndSupport;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Buy.Workflows.ShopinBit;

public partial class ConfirmPrivacyPolicyInputValidator : InputValidator
{
	[AutoNotify] private bool _hasAcceptedPrivacyPolicy;
	[AutoNotify] private LinkViewModel _link;

	public ConfirmPrivacyPolicyInputValidator(
		WorkflowState workflowState,
		LinkViewModel link,
		Func<string?> message,
		string content = "Accept")
		: base(workflowState, message, null, content)
	{
		_link = link;

		this.WhenAnyValue(x => x.HasAcceptedPrivacyPolicy)
			.Subscribe(_ => WorkflowState.SignalValid(IsValid()));
	}

	public override bool IsValid()
	{
		return HasAcceptedPrivacyPolicy;
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
