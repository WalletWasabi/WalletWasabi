using DynamicData;
using Nito.AsyncEx;
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
	[AutoNotify] private bool _goodPrivacy;
	[AutoNotify] private bool _maxPrivacy;

	private CancellationTokenSource? _suggestionCancellationTokenSource;

	/// <summary>Allow at most one suggestion generation run.</summary>
	private readonly AsyncLock _asyncLock = new();

	public PrivacySuggestionsFlyoutViewModel(Wallet wallet)
	{
		_privacySuggestionsModel = new PrivacySuggestionsModel(wallet);
	}

	public ObservableCollection<PrivacyWarning> Warnings { get; } = new();
	public ObservableCollection<PrivacySuggestion> Suggestions { get; } = new();

	/// <remarks>Method supports being called multiple times. In that case the last call cancels the previous one.</remarks>
	public async Task BuildPrivacySuggestionsAsync(TransactionInfo info, BuildTransactionResult transaction, CancellationToken cancellationToken)
	{
		using CancellationTokenSource singleRunCts = new();

		_suggestionCancellationTokenSource?.Cancel();
		using (await _asyncLock.LockAsync(CancellationToken.None))
		{
			_suggestionCancellationTokenSource = singleRunCts;
			using CancellationTokenSource timeoutCts = new(TimeSpan.FromSeconds(15));
			using CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(singleRunCts.Token, timeoutCts.Token, cancellationToken);

			NoPrivacy = false;
			MaxPrivacy = false;
			GoodPrivacy = false;
			Warnings.Clear();
			Suggestions.Clear();
			SelectedSuggestion = null;

			IsBusy = true;

			var result = await _privacySuggestionsModel.BuildPrivacySuggestionsAsync(info, transaction, linkedCts);

			Warnings.AddRange(result.Warnings);
			Suggestions.AddRange(result.Suggestions);

			if (Warnings.Any(x => x.Severity == WarningSeverity.Warning))
			{
				NoPrivacy = true;
			}
			else if (Warnings.Any(x => x.Severity == WarningSeverity.Info))
			{
				GoodPrivacy = true;
			}
			else
			{
				MaxPrivacy = true;
			}
			_suggestionCancellationTokenSource = null;
			IsBusy = false;
		}
	}
}
