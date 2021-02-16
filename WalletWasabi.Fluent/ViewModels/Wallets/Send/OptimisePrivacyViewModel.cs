using System.Collections.ObjectModel;
using WalletWasabi.Fluent.ViewModels.Navigation;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Send
{
	[NavigationMetaData(Title = "Optimise your privacy")]
	public partial class OptimisePrivacyViewModel : RoutableViewModel
	{
		[AutoNotify] private ObservableCollection<PrivacySuggestionControlViewModel> _privacySuggestions;
		[AutoNotify] private PrivacySuggestionControlViewModel _selectedPrivacySuggestion;

		public OptimisePrivacyViewModel()
		{
			_privacySuggestions = new ObservableCollection<PrivacySuggestionControlViewModel>();

			_privacySuggestions.Add(new PrivacySuggestionControlViewModel
			{
				Title = "1.13 BTC",
				Caption = "Less",
				Benefits = new[]
				{
					"Send 0.01% Less",
					"Improved Privacy",
					"Save on transaction fees"
				},
				OptimisationLevel = PrivacyOptimisationLevel.Better
			});

			_privacySuggestions.Add(new PrivacySuggestionControlViewModel
			{
				Title = "1.2 BTC",
				Caption = "Standard",
				Benefits = new[]
				{
					"Send Exact Amount"
				},
				OptimisationLevel = PrivacyOptimisationLevel.Standard
			});

			_privacySuggestions.Add(new PrivacySuggestionControlViewModel
			{
				Title = "1.27 BTC",
				Caption = "Extra",
				Benefits = new[]
				{
					"Send 0.01% More",
					"Improved Privacy",
					"Save on transaction fees"
				},
				OptimisationLevel = PrivacyOptimisationLevel.Better
			});

			SelectedPrivacySuggestion = _privacySuggestions[1];
		}
	}
}