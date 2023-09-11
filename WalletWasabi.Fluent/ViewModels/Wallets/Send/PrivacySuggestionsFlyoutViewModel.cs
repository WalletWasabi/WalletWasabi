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
	[AutoNotify] private bool _isBusy;

	[AutoNotify] private bool _noPrivacy;
	[AutoNotify] private bool _badPrivacy;
	[AutoNotify] private bool _goodPrivacy;
	[AutoNotify] private bool _maxPrivacy;

	public PrivacySuggestionsFlyoutViewModel(Wallet wallet)
	{
		_privacySuggestionsModel = new PrivacySuggestionsModel(wallet);
	}

	public ObservableCollection<PrivacyWarning> Warnings { get; } = new();
	public ObservableCollection<PrivacySuggestion> Suggestions { get; } = new();

	/// <remarks>Method supports being called multiple times. In that case the last call cancels the previous one.</remarks>
	public async Task BuildPrivacySuggestionsAsync(TransactionInfo info, BuildTransactionResult transaction, CancellationToken cancellationToken)
	{
		NoPrivacy = false;
		BadPrivacy = false;
		MaxPrivacy = false;
		GoodPrivacy = false;
		Warnings.Clear();
		Suggestions.Clear();
		SelectedSuggestion = null;

		IsBusy = true;

		var result = await _privacySuggestionsModel.BuildPrivacySuggestionsAsync(info, transaction, cancellationToken);

		await foreach (var warning in result.GetAllWarningsAsync())
		{
			Warnings.Add(warning);
		}

		await foreach (var suggestion in result.GetAllSuggestionsAsync())
		{
			Suggestions.Add(suggestion);
		}

		if (Warnings.Any(x => x.Severity == WarningSeverity.Critical))
		{
			NoPrivacy = true;
		}
		else if (Warnings.Any(x => x.Severity == WarningSeverity.Warning))
		{
			BadPrivacy = true;
		}
		else if (Warnings.Any(x => x.Severity == WarningSeverity.Info))
		{
			GoodPrivacy = true;
		}
		else
		{
			MaxPrivacy = true;
		}

		IsBusy = false;
	}
}
