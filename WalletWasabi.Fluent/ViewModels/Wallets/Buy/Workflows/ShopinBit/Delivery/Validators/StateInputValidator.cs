using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using ReactiveUI;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Buy.Workflows.ShopinBit;

public partial class StateInputValidator : TextInputInputValidator
{
	private readonly DeliveryWorkflowRequest _deliveryWorkflowRequest;
	private WebClients.ShopWare.Models.State[] _statesSource;

	[AutoNotify] private ObservableCollection<string> _states;
	[AutoNotify(SetterModifier = AccessModifier.Private)] private ObservableCollection<string> _state;


	public StateInputValidator(
		IWorkflowValidator workflowValidator,
		IShopinBitDataProvider shopinBitDataProvider,
		DeliveryWorkflowRequest deliveryWorkflowRequest,
		CancellationToken cancellationToken)
		: base(workflowValidator, null, "Type here...")
	{
		_deliveryWorkflowRequest = deliveryWorkflowRequest;

		var country = shopinBitDataProvider.GetCurrentCountry();

		// TODO: Make this async.
		_statesSource = shopinBitDataProvider.GetStatesForCountryAsync(country.Name, cancellationToken).GetAwaiter().GetResult();

		try
		{
			_states = new ObservableCollection<string>(_statesSource.Select(x => x.Name));
		}
		catch (Exception ex)
		{
			Console.WriteLine(ex);
		}

		_state = new ObservableCollection<string>();

		this.WhenAnyValue(x => x.State.Count)
			.Subscribe(_ => WorkflowValidator.Signal(IsValid()));
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
