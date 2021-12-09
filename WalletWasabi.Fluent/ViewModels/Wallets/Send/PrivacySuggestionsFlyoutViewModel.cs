using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using ReactiveUI;
using WalletWasabi.Blockchain.TransactionBuilding;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Send;

public partial class PrivacySuggestionsFlyoutViewModel : ViewModelBase
{
	[AutoNotify] private SuggestionViewModel? _previewSuggestion;
	[AutoNotify] private SuggestionViewModel? _selectedSuggestion;
	[AutoNotify] private bool _isOpen;
	[AutoNotify] private bool _isLoading;

	public PrivacySuggestionsFlyoutViewModel()
	{
		Suggestions = new ObservableCollection<SuggestionViewModel>();

		this.WhenAnyValue(x => x.IsOpen)
			.Subscribe(x =>
			{
				if (!x)
				{
					PreviewSuggestion = null;
				}
			});
	}

	public ObservableCollection<SuggestionViewModel> Suggestions { get; }

	public  async Task BuildPrivacySuggestionsAsync(Wallet wallet, TransactionInfo info, BuildTransactionResult transaction)
	{
		IsLoading = true;

		var (selected, suggestions) =
			await ChangeAvoidanceSuggestionViewModel.GenerateSuggestionsAsync(info, wallet, transaction);


		Suggestions.Clear();
		SelectedSuggestion = null;

		foreach (var suggestion in suggestions.Where(x => !x.IsOriginal))
		{
			Suggestions.Add(suggestion);
		}

		PreviewSuggestion = selected;

		IsLoading = false;
	}
}
