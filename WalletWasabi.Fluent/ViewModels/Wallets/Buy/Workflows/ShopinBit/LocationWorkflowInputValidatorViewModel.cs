using System.Collections.ObjectModel;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Buy.Workflows.ShopinBit;

public partial class LocationWorkflowInputValidatorViewModel : WorkflowInputValidatorViewModel
{
	[AutoNotify] private ObservableCollection<string> _countries;
	[AutoNotify] private ObservableCollection<string> _country;

	public LocationWorkflowInputValidatorViewModel() : base(null)
	{
		// TODO: Get from service.
		_countries = new ObservableCollection<string>()
		{
			"Austria",
			"Belgium",
			"Bulgaria",
			"Croatia",
			"Cyprus",
			"Czech Republic",
			"Denmark",
			"Estonia",
			"Finland",
			"France",
			"Germany",
			"Greece",
			"Hungary",
			"Ireland",
			"Italy",
			"Latvia",
			"Lithuania",
			"Luxembourg",
			"Malta",
			"Netherlands",
			"Poland",
			"Portugal",
			"Romania",
			"Slovakia",
			"Slovenia",
			"Spain",
			"Sweden",
			"Canada",
			"Switzerland",
			"United Kingdom",
			"United States of America",
		};
		_country = new ObservableCollection<string>();
	}

	public override bool IsValid(string message)
	{
		// TODO: Validate location.
		return !string.IsNullOrWhiteSpace(message);
	}
}
