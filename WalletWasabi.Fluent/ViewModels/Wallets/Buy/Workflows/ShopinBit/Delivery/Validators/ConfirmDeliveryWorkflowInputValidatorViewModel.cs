using System.Windows.Input;
using ReactiveUI;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Buy.Workflows.ShopinBit;

public partial class ConfirmDeliveryWorkflowInputValidatorViewModel : WorkflowInputValidatorViewModel
{
	private readonly DeliveryWorkflowRequest _deliveryWorkflowRequest;
	private bool _isConfirmed;

	public ConfirmDeliveryWorkflowInputValidatorViewModel(
		IWorkflowValidator workflowValidator,
		DeliveryWorkflowRequest deliveryWorkflowRequest)
		: base(workflowValidator, null, null)
	{
		// TODO: Show delivery summary in assistant message.
		_deliveryWorkflowRequest = deliveryWorkflowRequest;

		ConfirmDeliveryCommand = ReactiveCommand.Create(() =>
		{
			_isConfirmed = true;
			WorkflowValidator.Signal(IsValid());
		});
	}

	public ICommand ConfirmDeliveryCommand { get; }

	public override bool IsValid()
	{
		return _isConfirmed;
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
