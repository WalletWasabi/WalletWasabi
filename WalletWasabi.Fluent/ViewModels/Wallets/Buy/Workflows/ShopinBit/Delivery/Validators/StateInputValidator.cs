using System.Collections.ObjectModel;
using System.Linq;
using ReactiveUI;
using WalletWasabi.BuyAnything;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Buy.Workflows.ShopinBit;

public partial class StateInputValidator : InputValidator
{
	private readonly DeliveryWorkflowRequest _deliveryWorkflowRequest;
	private WebClients.ShopWare.Models.State[] _statesSource;

	[AutoNotify] private ObservableCollection<string> _states;
	[AutoNotify(SetterModifier = AccessModifier.Private)] private ObservableCollection<string> _state;

	public StateInputValidator(
		WorkflowState workflowState,
		WebClients.ShopWare.Models.State[] states,
		DeliveryWorkflowRequest deliveryWorkflowRequest,
		ChatMessageMetaData.ChatMessageTag tag)
		: base(workflowState, null, "Type here...", "Next", tag: tag)
	{
		_deliveryWorkflowRequest = deliveryWorkflowRequest;
		_statesSource = states;
		_states = new ObservableCollection<string>(_statesSource.Select(x => x.Name));
		_state = new ObservableCollection<string>();

		this.WhenAnyValue(x => x.State.Count)
			.Subscribe(_ => WorkflowState.SignalValid(IsValid()));
	}

	public override bool IsValid()
	{
		// TODO: Validate request.
		return _state.Count == 1 && !string.IsNullOrWhiteSpace(_state[0]);
	}

	public override string? GetFinalMessage()
	{
		if (IsValid())
		{
			var state = _statesSource[_states.IndexOf(_state[0])];

			_deliveryWorkflowRequest.State = state;

			return _state[0];
		}

		return null;
	}
}
