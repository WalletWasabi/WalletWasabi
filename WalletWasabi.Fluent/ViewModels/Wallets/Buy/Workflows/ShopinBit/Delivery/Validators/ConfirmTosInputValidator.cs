using ReactiveUI;
using WalletWasabi.Fluent.ViewModels.HelpAndSupport;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Buy.Workflows.ShopinBit;

public partial class ConfirmTosInputValidator : InputValidator
{
	private readonly DeliveryWorkflowRequest _deliveryWorkflowRequest;

	[AutoNotify] private bool _hasAcceptedTermsOfService;
	[AutoNotify] private LinkViewModel _link;

	public ConfirmTosInputValidator(
		IWorkflowValidator workflowValidator,
		DeliveryWorkflowRequest deliveryWorkflowRequest,
		LinkViewModel link,
		string message,
		string content)
		: base(workflowValidator, message, null, content)
	{
		_deliveryWorkflowRequest = deliveryWorkflowRequest;
		_link = link;

		this.WhenAnyValue(x => x.HasAcceptedTermsOfService)
			.Subscribe(_ => WorkflowValidator.Signal(IsValid()));
	}

	public override bool IsValid()
	{
		return HasAcceptedTermsOfService;
	}

	public override string? GetFinalMessage()
	{
		if (IsValid())
		{
			_deliveryWorkflowRequest.HasAcceptedTermsOfService = HasAcceptedTermsOfService;

			return Message;
		}

		return null;
	}
}
