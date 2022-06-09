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
using WalletWasabi.Blockchain.TransactionBuilding.BnB;
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

		// Exchange rate can change substantially during computation itself.
		// Reporting up-to-date exchange rates would just confuse users.
		decimal usdExchangeRate = wallet.Synchronizer.UsdExchangeRate;

		var hasChange = transaction.InnerWalletOutputs.Any(x => x.ScriptPubKey != destination.ScriptPubKey);

		var onlyPrivateCoinsUsed = transaction.SpentCoins.All(x => x.HdPubKey.AnonymitySet > wallet.KeyManager.AnonScoreTarget);

		if (hasChange && onlyPrivateCoinsUsed && !isFixedAmount && !info.IsPayJoin)
		{
			int originalInputCount = transaction.SpentCoins.Count();
			int maxInputCount = (int)Math.Max(3, originalInputCount * 1.3);

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
		else if (hasChange && !isFixedAmount && !info.IsPayJoin)
		{
			// If non-private coins were used to build the original transaction, show only basic suggestions, otherwise we might do more harm than good.

			BuildTransactionResult largerTransaction = TransactionHelpers.BuildChangelessTransaction(
					wallet,
					destination,
					info.UserLabels,
					info.FeeRate,
					transaction.SpentCoins,
					tryToSign: false);

			var largerSuggestion = new ChangeAvoidanceSuggestionViewModel(info.Amount.ToDecimal(MoneyUnit.BTC), largerTransaction, usdExchangeRate);

			// Sanity check not to show crazy suggestions
			if (largerTransaction.CalculateDestinationAmount().Satoshi < MoreSelectionStrategy.MaxExtraPayment * transaction.CalculateDestinationAmount().Satoshi)
			{
				Suggestions.Insert(Suggestions.Count - 1, largerSuggestion);
			}

			ChangeAvoidanceSuggestionViewModel? smallerSuggestion = null;
			if (transaction.SpentCoins.Count() > 1)
			{
				BuildTransactionResult smallerTransaction = TransactionHelpers.BuildChangelessTransaction(
					wallet,
					destination,
					info.UserLabels,
					info.FeeRate,
					transaction
						.SpentCoins
						.OrderByDescending(x => x.Amount)
						.Skip(1),
					tryToSign: false);

				smallerSuggestion = new ChangeAvoidanceSuggestionViewModel(
					info.Amount.ToDecimal(MoneyUnit.BTC),
					smallerTransaction,
					usdExchangeRate);

				// Sanity check not to show crazy suggestions
				if (smallerTransaction.CalculateDestinationAmount().Satoshi > LessSelectionStrategy.MinPaymentThreshold * transaction.CalculateDestinationAmount().Satoshi)
				{
					Suggestions.Insert(Suggestions.Count - 1, smallerSuggestion);
				}
			}
		}

		Suggestions.Remove(loadingRing);
	}
}
