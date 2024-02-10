using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using ReactiveUI;
using WalletWasabi.BuyAnything;
using CountryState = WalletWasabi.WebClients.ShopWare.Models.State;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Buy.Workflows;

public partial class StateStep : WorkflowStep<CountryState>
{
	private readonly CancellationToken _token;
	[AutoNotify] private ObservableCollection<string> _states = new();
	[AutoNotify] private ObservableCollection<string> _selectedStates = new();

	private CountryState[] _statesArray = Array.Empty<CountryState>();

	public StateStep(Conversation conversation, CancellationToken token, bool isEditing = false) : base(conversation, token, isEditing)
	{
		_token = token;
		Watermark = "Type in a state...";

		// TODO: TagsBox provide a list as result, so it cannot be directly bound to Value. Fede, better idea?
		this.WhenAnyValue(x => x.SelectedStates.Count)
			.Select(_ => SelectedStates.FirstOrDefault())
			.Select(stateString => _statesArray.FirstOrDefault(x => x.Name == stateString))
			.BindTo(this, x => x.Value);
	}

	protected override IEnumerable<string> BotMessages(Conversation conversation)
	{
		yield return "State:";
	}

	public override async Task ExecuteAsync()
	{
		if (_ignored)
		{
			return;
		}

		if (Conversation.MetaData.Country is { } country)
		{
			IsBusy = true;

			var buyAnythingManager = Services.HostedServices.Get<BuyAnythingManager>();

			_statesArray = await buyAnythingManager.GetStatesForCountryAsync(country, _token);
			States = new ObservableCollection<string>(_statesArray.Select(x => x.Name));

			if (!States.Any())
			{
				Ignore();
			}

			IsBusy = false;
		}

		await base.ExecuteAsync();
	}

	public override async Task<ChatMessage> EditMessageAsync(ChatMessage chatMessage)
	{
		if (Conversation.MetaData.Country is { } country)
		{
			IsBusy = true;

			var buyAnythingManager = Services.HostedServices.Get<BuyAnythingManager>();

			_statesArray = await buyAnythingManager.GetStatesForCountryAsync(country, _token);
			States = new ObservableCollection<string>(_statesArray.Select(x => x.Name));

			if (!States.Any())
			{
				Ignore();
			}
			else
			{
				SelectedStates = new ObservableCollection<string>(new[] { chatMessage.Text });
			}

			IsBusy = false;
		}

		return await base.EditMessageAsync(chatMessage);
	}

	protected override Conversation PutValue(Conversation conversation, CountryState value) =>
		conversation.UpdateMetadata(m => m with { State = value });

	protected override CountryState? RetrieveValue(Conversation conversation) => conversation.MetaData.State;

	protected override string? StringValue(CountryState value) => value.Name;
}
