using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Blockchain.TransactionBuilding;
using WalletWasabi.Fluent.Models.Transactions;
using WalletWasabi.Fluent.Models.Wallets;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Send;

public partial class PrivacySuggestionsFlyoutViewModel : ViewModelBase
{
	private readonly PrivacySuggestionsModel _privacySuggestionsModel;
	private readonly Subject<IEnumerable<PrivacyWarning>> _previewWarnings = new();

	[AutoNotify] private PrivacySuggestion? _previewSuggestion;
	[AutoNotify] private PrivacySuggestion? _selectedSuggestion;
	[AutoNotify] private bool _isBusy;

	[AutoNotify] private bool _noPrivacy;
	[AutoNotify] private bool _badPrivacy;
	[AutoNotify] private bool _goodPrivacy;
	[AutoNotify] private bool _maxPrivacy;

	public PrivacySuggestionsFlyoutViewModel(IWalletModel wallet, SendFlowModel sendParameters)
	{
		_privacySuggestionsModel = wallet.GetPrivacySuggestionsModel(sendParameters);
	}

	public ObservableCollection<PrivacyWarning> Warnings { get; } = new();
	public ObservableCollection<PrivacySuggestion> Suggestions { get; } = new();
	public IObservable<IEnumerable<PrivacyWarning>> PreviewWarnings => _previewWarnings;

	public async Task UpdatePreviewWarningsAsync(TransactionInfo info, BuildTransactionResult transaction, CancellationToken cancellationToken)
	{
		var previewWarningList = new List<PrivacyWarning>();

		await foreach (var item in _privacySuggestionsModel.BuildPrivacySuggestionsAsync(info, transaction, cancellationToken, includeSuggestions: false))
		{
			if (item is PrivacyWarning warning)
			{
				previewWarningList.Add(warning);
			}
		}

		_previewWarnings.OnNext(previewWarningList);
	}

	public void ClearPreviewWarnings()
	{
		_previewWarnings.OnNext(Warnings);
	}

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

		await foreach (var item in _privacySuggestionsModel.BuildPrivacySuggestionsAsync(info, transaction, cancellationToken, includeSuggestions: true))
		{
			if (item is PrivacyWarning warning)
			{
				Warnings.Add(warning);
			}
			if (item is PrivacySuggestion suggestion)
			{
				Suggestions.Add(suggestion);
			}
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
