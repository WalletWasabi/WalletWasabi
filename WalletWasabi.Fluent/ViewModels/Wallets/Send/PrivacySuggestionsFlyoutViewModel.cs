using DynamicData;
using ReactiveUI;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Blockchain.TransactionBuilding;
using WalletWasabi.Fluent.Models.Transactions;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Send;

public partial class PrivacySuggestionsFlyoutViewModel : ViewModelBase
{
	private readonly PrivacySuggestionsModel _privacySuggestionsModel;

	[AutoNotify] private PrivacySuggestion? _previewSuggestion;
	[AutoNotify] private PrivacySuggestion? _selectedSuggestion;
	[AutoNotify] private bool _isOpen;
	[AutoNotify] private bool _isVisible;
	[AutoNotify] private bool _isBusy;

	public PrivacySuggestionsFlyoutViewModel(Wallet wallet)
	{
		_privacySuggestionsModel = new PrivacySuggestionsModel(wallet);

		this.WhenAnyValue(x => x.IsOpen)
			.Subscribe(x =>
			{
				if (!x)
				{
					PreviewSuggestion = null;
				}
			});
	}

	public ObservableCollection<PrivacyWarning> Warnings { get; } = new();
	public ObservableCollection<PrivacySuggestion> Suggestions { get; } = new();

	/// <remarks>Method supports being called multiple times. In that case the last call cancels the previous one.</remarks>
	public async Task BuildPrivacySuggestionsAsync(TransactionInfo info, BuildTransactionResult transaction, CancellationToken cancellationToken)
	{
		Warnings.Clear();
		Suggestions.Clear();

		SelectedSuggestion = null;

		IsVisible = true;
		IsBusy = true;

		var result = await _privacySuggestionsModel.BuildPrivacySuggestionsAsync(info, transaction, cancellationToken);

		Warnings.AddRange(result.Warnings);
		Suggestions.AddRange(result.Suggestions);

		IsBusy = false;
		IsVisible = Warnings.Any() || Suggestions.Any();

		if (!IsVisible)
		{
			IsOpen = false;
		}
	}
}
