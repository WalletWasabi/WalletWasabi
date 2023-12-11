using System.Collections.ObjectModel;
using System.Linq;
using ReactiveUI;
using WalletWasabi.BuyAnything;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Buy.Workflows.ShopinBit;

public partial class LocationInputValidator : InputValidator
{

	[AutoNotify] private ObservableCollection<string> _countries;
	[AutoNotify(SetterModifier = AccessModifier.Private)] private ObservableCollection<string> _country;

	public LocationInputValidator(
		WorkflowState workflowState,
		Country[] countries,
		ChatMessageMetaData.ChatMessageTag tag)
		: base(workflowState, null, "Enter your location...", "Next", tag)
	{
		_countries = new ObservableCollection<string>(countries.Select(x => x.Name));
		_country = new ObservableCollection<string>();

		this.WhenAnyValue(x => x.Country.Count)
			.Subscribe(_ => WorkflowState.SignalValid(IsValid()));
	}

	public override bool IsValid()
	{
		return _country.Count == 1 && !string.IsNullOrWhiteSpace(_country[0]);
	}

	public override string? GetFinalMessage()
	{
		if (IsValid())
		{
			return _country[0];
		}

		return null;
	}
}
