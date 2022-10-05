using NBitcoin;
using Nito.AsyncEx;
using ReactiveUI;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using WalletWasabi.Blockchain.TransactionBuilding;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.Models;
using WalletWasabi.Logging;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Send;

public partial class PrivacySuggestionsFlyoutViewModel : ViewModelBase
{
	/// <remarks>Guards use of <see cref="_suggestionCancellationTokenSource"/>.</remarks>
	private readonly object _lock = new();

	/// <summary>Allow at most one suggestion generation run.</summary>
	private readonly AsyncLock _asyncLock = new();

	[AutoNotify] private SuggestionViewModel? _previewSuggestion;
	[AutoNotify] private SuggestionViewModel? _selectedSuggestion;
	[AutoNotify] private bool _isOpen;
	[AutoNotify] private bool _canShowOtherSuggestions;

	private CancellationTokenSource? _suggestionCancellationTokenSource;

	public PrivacySuggestionsFlyoutViewModel()
	{
		Suggestions = new();

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

	/// <remarks>Method supports being called multiple times. In that case the last call cancels the previous one.</remarks>
	public async Task BuildPrivacySuggestionsAsync(Wallet wallet, TransactionInfo info, BitcoinAddress destination, BuildTransactionResult transaction, bool isFixedAmount, bool showOtherSuggestions, CancellationToken cancellationToken)
	{
		using CancellationTokenSource singleRunCts = new();

		lock (_lock)
		{
			_suggestionCancellationTokenSource?.Cancel();
			_suggestionCancellationTokenSource = singleRunCts;
		}

		using CancellationTokenSource timeoutCts = new(TimeSpan.FromSeconds(15));
		using CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(timeoutCts.Token, singleRunCts.Token, cancellationToken);

		using (await _asyncLock.LockAsync(CancellationToken.None))
		{
			try
			{
				Suggestions.Clear();
				SelectedSuggestion = null;
				CanShowOtherSuggestions = false;

				var loadingRing = new LoadingSuggestionViewModel();
				Suggestions.Add(loadingRing);

				await BuildMainPrivacySuggestionsAsync(wallet, info, destination, transaction, isFixedAmount, linkedCts);

				if (Suggestions.Count == 1 || showOtherSuggestions)
				{
					CanShowOtherSuggestions = false;

					await BuildChangeAvoidanceSuggestionsAsync(wallet, info, destination, transaction, isFixedAmount, linkedCts);
				}
				else
				{
					CanShowOtherSuggestions = true;
				}

				Suggestions.Remove(loadingRing);
			}
			catch (OperationCanceledException)
			{
				Logger.LogTrace("Operation was cancelled.");
			}
			finally
			{
				lock (_lock)
				{
					_suggestionCancellationTokenSource = null;
				}
			}
		}
	}

	private async Task BuildMainPrivacySuggestionsAsync(Wallet wallet, TransactionInfo info, BitcoinAddress destination, BuildTransactionResult transaction, bool isFixedAmount, CancellationTokenSource linkedCts)
	{
		// Unsafe coins: coins that either don't match the recipient labels, or that are below green privacy level
		var unsafeCoins =
			transaction.SpentCoins.Where(c => c.GetLabels(wallet.KeyManager.AnonScoreTarget) != info.UserLabels)
								  .Where(x => x.GetPrivacyLevel(wallet) != PrivacyLevel.Private)
								  .ToList();

		if (unsafeCoins.Any())
		{
			// Safe coins: coins that either match the recipient labels, or that have green privacy level
			var safeCoins =
				transaction.SpentCoins.Where(c => c.GetLabels(wallet.KeyManager.AnonScoreTarget) == info.UserLabels || c.GetPrivacyLevel(wallet) == PrivacyLevel.Private)
									  .ToList();

			if (safeCoins.Any())
			{
				// Exchange rate can change substantially during computation itself.
				// Reporting up-to-date exchange rates would just confuse users.
				decimal usdExchangeRate = wallet.Synchronizer.UsdExchangeRate;

				// Only allow to create 1 more input with BnB. This accounts for the change created.
				int maxInputCount = safeCoins.Count + 1;

				var coinsToUse = safeCoins.ToImmutableArray();

				// Clone the transaction and alter the amount to the available private
				var transactionInfo = info.Clone();
				transactionInfo.Amount = safeCoins.Sum(x => x.Amount);

				var suggestions = TransactionHelpers.GeneratePrivacySuggestionTransactionsAsync(transactionInfo, destination, wallet, coinsToUse, maxInputCount, linkedCts.Token);

				await foreach (var suggestion in suggestions)
				{
					var suggestionVm =
						new SendOnlySafeCoinsSuggestionViewModel(info.Amount.ToDecimal(MoneyUnit.BTC), suggestion, usdExchangeRate);

					Suggestions.Insert(Suggestions.Count - 1, suggestionVm);
				}
			}

			Suggestions.Insert(Suggestions.Count - 1, new CoinjoinMoreSuggestionViewModel(wallet));
		}
	}

	private async Task BuildChangeAvoidanceSuggestionsAsync(Wallet wallet, TransactionInfo info, BitcoinAddress destination, BuildTransactionResult transaction, bool isFixedAmount, CancellationTokenSource linkedCts)
	{
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

			var suggestions = TransactionHelpers.GeneratePrivacySuggestionTransactionsAsync(info, destination, wallet, coinsToUse, maxInputCount, linkedCts.Token);

			await foreach (var suggestion in suggestions)
			{
				var suggestionVm =
					new ChangeAvoidanceSuggestionViewModel(info.Amount.ToDecimal(MoneyUnit.BTC), suggestion, usdExchangeRate);

				Suggestions.Insert(Suggestions.Count - 1, suggestionVm);
			}
		}
	}
}
