using System.Collections.ObjectModel;
using System.Linq;
using ReactiveUI;
using WalletWasabi.BuyAnything;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Buy.Workflows.ShopinBit;

public partial class StateInputValidator : InputValidator
{
	private WebClients.ShopWare.Models.State[] _statesSource;

	[AutoNotify] private ObservableCollection<string> _states;
	[AutoNotify(SetterModifier = AccessModifier.Private)] private ObservableCollection<string> _state;

	public StateInputValidator(
		WorkflowState workflowState,
		WebClients.ShopWare.Models.State[] states,
		ChatMessageMetaData.ChatMessageTag tag)
		: base(workflowState, null, "Type here...", "Next", tag: tag)
	{
		_statesSource = states;
		_states = new ObservableCollection<string>(_statesSource.Select(x => x.Name));
		_state = new ObservableCollection<string>();

		this.WhenAnyValue(x => x.State.Count)
			.Subscribe(_ => WorkflowState.SignalValid(IsValid()));
	}

	public override bool IsValid()
	{
		return _state.Count == 1 && !string.IsNullOrWhiteSpace(_state[0]);
	}

	public override string? GetFinalMessage()
	{
		if (IsValid())
		{
			return _state[0];
		}

		return null;
	}
}
