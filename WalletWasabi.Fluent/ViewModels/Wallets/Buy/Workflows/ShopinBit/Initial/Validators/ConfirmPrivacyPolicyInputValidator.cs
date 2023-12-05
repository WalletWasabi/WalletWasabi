using ReactiveUI;
using WalletWasabi.Fluent.ViewModels.HelpAndSupport;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Buy.Workflows.ShopinBit;

public partial class ConfirmPrivacyPolicyInputValidator : InputValidator
{
	private readonly InitialWorkflowRequest _initialWorkflowRequest;

	[AutoNotify] private bool _hasAcceptedPrivacyPolicy;
	[AutoNotify] private LinkViewModel _link;

	public ConfirmPrivacyPolicyInputValidator(
		IWorkflowValidator workflowValidator,
		InitialWorkflowRequest initialWorkflowRequest,
		LinkViewModel link,
		Func<string?> message,
		string content = "Accept")
		: base(workflowValidator, message, null, content)
	{
		_initialWorkflowRequest = initialWorkflowRequest;
		_link = link;

		this.WhenAnyValue(x => x.HasAcceptedPrivacyPolicy)
			.Subscribe(_ => WorkflowValidator.Signal(IsValid()));
	}

	public override bool IsValid()
	{
		return HasAcceptedPrivacyPolicy;
	}

	public override string? GetFinalMessage()
	{
		if (IsValid())
		{
			_initialWorkflowRequest.HasAcceptedPrivacyPolicy = HasAcceptedPrivacyPolicy;

			return Message;
		}

		return null;
	}
}
