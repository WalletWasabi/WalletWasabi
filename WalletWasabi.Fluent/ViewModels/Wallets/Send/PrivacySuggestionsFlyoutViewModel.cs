using Nito.AsyncEx;
using ReactiveUI;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Blockchain.TransactionBuilding;
using WalletWasabi.Fluent.Helpers;
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
	[AutoNotify] private bool _isVisible;
	[AutoNotify] private bool _isBusy;

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

	/// <remarks>Method supports being called multiple times. In that case the last call cancels the previous one.</remarks>
	public async Task BuildPrivacySuggestionsAsync(Wallet wallet, TransactionInfo info, BuildTransactionResult transaction, CancellationToken cancellationToken)
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

				IsVisible = true;
				IsBusy = true;

				var hasChange = transaction.InnerWalletOutputs.Any(x => x.ScriptPubKey != info.Destination.ScriptPubKey);

				if (hasChange && !info.IsFixedAmount && !info.IsPayJoin)
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
						ChangeAvoidanceSuggestionViewModel.GenerateSuggestionsAsync(info, wallet, coinsToUse, maxInputCount, usdExchangeRate, linkedCts.Token);

					await foreach (var suggestion in suggestions)
					{
						Suggestions.Add(suggestion);
					}
				}
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

				IsBusy = false;
				IsVisible = Suggestions.Any();
				if (!IsVisible)
				{
					IsOpen = false;
				}
			}
		}
	}
}
