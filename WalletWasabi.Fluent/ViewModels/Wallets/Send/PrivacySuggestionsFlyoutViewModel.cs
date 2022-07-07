using NBitcoin;
using ReactiveUI;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.TransactionBuilding;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Send;

public partial class PrivacySuggestionsFlyoutViewModel : ViewModelBase
{
	[AutoNotify] private SuggestionViewModel? _previewSuggestion;
	[AutoNotify] private SuggestionViewModel? _selectedSuggestion;
	[AutoNotify] private bool _isOpen;
	private CancellationTokenSource? _suggestionCancellationTokenSource;

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

	public async Task BuildPrivacySuggestionsAsync(Wallet wallet, TransactionInfo info, BitcoinAddress destination, BuildTransactionResult transaction, bool isFixedAmount, CancellationToken cancellationToken)
	{
		_suggestionCancellationTokenSource?.Cancel();
		_suggestionCancellationTokenSource?.Dispose();

		_suggestionCancellationTokenSource = new(TimeSpan.FromSeconds(15));
		using CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_suggestionCancellationTokenSource.Token, cancellationToken);

		Suggestions.Clear();
		SelectedSuggestion = null;

		var loadingRing = new LoadingSuggestionViewModel();
		Suggestions.Add(loadingRing);

		var hasChange = transaction.InnerWalletOutputs.Any(x => x.ScriptPubKey != destination.ScriptPubKey);

		if (hasChange && !isFixedAmount && !info.IsPayJoin)
		{
			// Exchange rate can change substantially during computation itself.
			// Reporting up-to-date exchange rates would just confuse users.
			decimal usdExchangeRate = wallet.Synchronizer.UsdExchangeRate;

			// Only allow to create 1 more input with BnB. This accounts for the change created.
			int maxInputCount = transaction.SpentCoins.Count() + 1;

			var pockets = wallet.GetPockets();
			var spentCoins = transaction.SpentCoins;
			var usedPockets = pockets.Where(x => x.Coins.Any(coin => spentCoins.Contains(coin)));
			var coinsToUse = usedPockets.SelectMany(x => x.Coins).ToImmutableArray();

			IAsyncEnumerable<ChangeAvoidanceSuggestionViewModel> suggestions =
				ChangeAvoidanceSuggestionViewModel.GenerateSuggestionsAsync(info, destination, wallet, coinsToUse, maxInputCount, usdExchangeRate, linkedCts.Token);

			await foreach (var suggestion in suggestions)
			{
				Suggestions.Insert(Suggestions.Count - 1, suggestion);
			}
		}

		Suggestions.Remove(loadingRing);
	}
}
