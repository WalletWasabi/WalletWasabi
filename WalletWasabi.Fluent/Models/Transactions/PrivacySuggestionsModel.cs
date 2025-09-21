using DynamicData;
using NBitcoin;
using Nito.AsyncEx;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.TransactionBuilding;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.ViewModels.Wallets.Send;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;
using WalletWasabi.WabiSabi.Client;
using WalletWasabi.Wallets;
using WalletWasabi.Wallets.Exchange;

namespace WalletWasabi.Fluent.Models.Transactions;

[AutoInterface]
public partial class PrivacySuggestionsModel
{
	private const decimal MaximumDifferenceTolerance = Constants.BnBMaximumDifferenceTolerance;

	private const decimal LargePortionSpentTolerance = 0.9m;
	private static readonly Money LargePortionSpentMinAmount = new (1_000_000L);

	/// <remarks>Guards use of <see cref="_singleRunCancellationTokenSource"/>.</remarks>
	private readonly object _lock = new();

	/// <summary>Allow at most one suggestion generation run.</summary>
	private readonly AsyncLock _asyncLock = new();

	private readonly CoinJoinManager? _cjManager;
	private readonly SendFlowModel _sendFlow;
	private readonly ExchangeRateUpdater _exchangeRateUpdater;
	private readonly Wallet _wallet;
	private CancellationTokenSource? _singleRunCancellationTokenSource;
	private CancellationTokenSource? _linkedCancellationTokenSource;

	public PrivacySuggestionsModel(SendFlowModel sendFlow)
	{
		_sendFlow = sendFlow;
		_wallet = sendFlow.Wallet;
		_cjManager = Services.HostedServices.GetOrDefault<CoinJoinManager>();
		_exchangeRateUpdater = Services.HostedServices.Get<ExchangeRateUpdater>();
	}

	/// <summary>
	///
	/// </summary>
	/// <remarks>Method supports being called multiple times. In that case the last call cancels the previous one.</remarks>
	public async IAsyncEnumerable<PrivacyItem> BuildPrivacySuggestionsAsync(TransactionInfo transactionInfo, BuildTransactionResult transactionResult, [EnumeratorCancellation] CancellationToken cancellationToken, bool includeSuggestions)
	{
		var parameters = new Parameters(transactionInfo, transactionResult, includeSuggestions);
		var result = new List<PrivacyItem>();

		using CancellationTokenSource singleRunCts = new();

		lock (_lock)
		{
			_singleRunCancellationTokenSource?.Cancel();
			_linkedCancellationTokenSource?.Cancel();
			_singleRunCancellationTokenSource = singleRunCts;
			CancellationTokenSource timeoutCts = new(TimeSpan.FromSeconds(15));
			CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(timeoutCts.Token, singleRunCts.Token, cancellationToken);
			_linkedCancellationTokenSource = linkedCts;
		}

		using (await _asyncLock.LockAsync(CancellationToken.None))
		{
			result.Add(VerifyLabels(parameters));
			result.Add(VerifyPrivacyLevel(parameters));
			result.Add(VerifyLargePercentSpent(parameters));
			result.Add(VerifyUnconfirmedInputs(parameters));
			result.Add(VerifyCoinjoiningInputs(parameters));
			foreach (var item in result)
			{
				yield return item;
			}
			await foreach (var item in VerifyChangeAsync(parameters, _linkedCancellationTokenSource).ConfigureAwait(false))
			{
				yield return item;
			}
			lock (_lock)
			{
				_singleRunCancellationTokenSource = null;
			}
		}
	}

	private IEnumerable<PrivacyItem> VerifyLabels(Parameters parameters)
	{
		var warning = GetLabelWarning(parameters.Transaction, parameters.TransactionInfo.Recipient);

		if (warning is not null)
		{
			yield return warning;

			if (parameters.IncludeSuggestions && parameters.TransactionInfo.IsOtherPocketSelectionPossible)
			{
				yield return new LabelManagementSuggestion();
			}
		}
	}

	private PrivacyItem? GetLabelWarning(BuildTransactionResult transactionResult, LabelsArray recipient)
	{
		var pockets = _sendFlow.GetPockets();
		var spentCoins = transactionResult.SpentCoins;
		var nonPrivateSpentCoins = spentCoins.Where(x => x.GetPrivacyLevel(_wallet.AnonScoreTarget) == PrivacyLevel.NonPrivate).ToList();
		var usedPockets = pockets.Where(x => x.Coins.Any(coin => nonPrivateSpentCoins.Contains(coin))).ToList();

		if (usedPockets.Count > 1)
		{
			var pocketLabels = usedPockets.SelectMany(x => x.Labels).Distinct().ToArray();
			return new InterlinksLabelsWarning(new LabelsArray(pocketLabels));
		}

		var labels = transactionResult.SpentCoins.SelectMany(x => x.GetLabels(_wallet.AnonScoreTarget)).Except(recipient);
		var labelsArray = new LabelsArray(labels);

		if (labelsArray.Any())
		{
			return new TransactionKnownAsYoursByWarning(labelsArray);
		}

		return null;
	}

	private IEnumerable<PrivacyItem> VerifyPrivacyLevel(Parameters parameters)
	{
		var canModifyTransactionAmount = !parameters.TransactionInfo.IsPayJoin && !parameters.TransactionInfo.IsFixedAmount;

		var transactionLabels = parameters.Transaction.SpentCoins.SelectMany(x => x.GetLabels(_wallet.AnonScoreTarget));
		var onlyKnownByRecipient =
			parameters.TransactionInfo.Recipient.Equals(new LabelsArray(transactionLabels), StringComparer.OrdinalIgnoreCase);

		var foundNonPrivate = !onlyKnownByRecipient &&
							  parameters.Transaction.SpentCoins.Any(x => x.GetPrivacyLevel(_wallet.AnonScoreTarget) == PrivacyLevel.NonPrivate);

		var foundSemiPrivate =
			parameters.Transaction.SpentCoins.Any(x => x.GetPrivacyLevel(_wallet.AnonScoreTarget) == PrivacyLevel.SemiPrivate);

		if (foundNonPrivate)
		{
			yield return new NonPrivateFundsWarning();
		}

		if (foundSemiPrivate)
		{
			yield return new SemiPrivateFundsWarning();
		}

		if (!parameters.IncludeSuggestions)
		{
			// Return early, to avoid needless compute.
			yield break;
		}

		var availableCoins = _sendFlow.AvailableCoins;

		ImmutableList<SmartCoin> coinsToExclude = _cjManager?.CoinsInCriticalPhase[_wallet.WalletId] ?? [];
		bool wasCoinjoiningCoinUsed = parameters.Transaction.SpentCoins.Any(coinsToExclude.Contains);

		// Only exclude coins if the original transaction doesn't use them either.
		var allPrivateCoin = availableCoins.Where(x => x.GetPrivacyLevel(_wallet.AnonScoreTarget) == PrivacyLevel.Private).ToArray();

		allPrivateCoin = wasCoinjoiningCoinUsed ? allPrivateCoin : allPrivateCoin.Except(coinsToExclude).ToArray();

		var onlyKnownByTheRecipientCoins = availableCoins.Where(x => parameters.TransactionInfo.Recipient.Equals(x.GetLabels(_wallet.AnonScoreTarget), StringComparer.OrdinalIgnoreCase)).ToArray();
		var allSemiPrivateCoin =
			availableCoins.Where(x => x.GetPrivacyLevel(_wallet.AnonScoreTarget) == PrivacyLevel.SemiPrivate)
						  .Union(onlyKnownByTheRecipientCoins)
						  .ToArray();

		allSemiPrivateCoin = wasCoinjoiningCoinUsed ? allSemiPrivateCoin : allSemiPrivateCoin.Except(coinsToExclude).ToArray();

		var usdExchangeRate = _exchangeRateUpdater.UsdExchangeRate;
		var totalAmount = parameters.Transaction.CalculateDestinationAmount(parameters.TransactionInfo.Destination).ToDecimal(MoneyUnit.BTC);
		FullPrivacySuggestion? fullPrivacySuggestion = null;

		if ((foundNonPrivate || foundSemiPrivate) && allPrivateCoin.Length != 0 &&
			TryCreateTransaction(parameters.TransactionInfo, allPrivateCoin, out var newTransaction, out var isChangeless))
		{
			var amountDifference = totalAmount - newTransaction.CalculateDestinationAmount(parameters.TransactionInfo.Destination).ToDecimal(MoneyUnit.BTC);
			var amountDifferencePercentage = amountDifference / totalAmount;

			if (amountDifferencePercentage <= MaximumDifferenceTolerance && (canModifyTransactionAmount || amountDifference == 0m))
			{
				var (differenceBtc, differenceFiat) = GetDifference(parameters.TransactionInfo, newTransaction, usdExchangeRate);
				var differenceText = GetDifferenceText(differenceBtc);
				var differenceAmountText = GetDifferenceAmountText(differenceBtc, differenceFiat);
				fullPrivacySuggestion = new FullPrivacySuggestion(newTransaction, amountDifference, differenceText, differenceAmountText, allPrivateCoin, isChangeless);
				yield return fullPrivacySuggestion;
			}
		}

		// Do not calculate the better privacy option when the full privacy option has the same amount.
		// This is only possible if the user makes a silly selection with coin control.
		if (fullPrivacySuggestion is { } sug && sug.Difference == 0m)
		{
			yield break;
		}

		var coins = allPrivateCoin.Union(allSemiPrivateCoin).ToArray();
		if (foundNonPrivate && allSemiPrivateCoin.Length != 0 &&
			TryCreateTransaction(parameters.TransactionInfo, coins, out newTransaction, out isChangeless))
		{
			var amountDifference = totalAmount - newTransaction.CalculateDestinationAmount(parameters.TransactionInfo.Destination).ToDecimal(MoneyUnit.BTC);
			var amountDifferencePercentage = amountDifference / totalAmount;

			if (amountDifferencePercentage <= MaximumDifferenceTolerance && (canModifyTransactionAmount || amountDifference == 0m))
			{
				var (btcDifference, fiatDifference) = GetDifference(parameters.TransactionInfo, newTransaction, usdExchangeRate);
				var differenceText = GetDifferenceText(btcDifference);
				var differenceAmountText = GetDifferenceAmountText(btcDifference, fiatDifference);
				yield return new BetterPrivacySuggestion(newTransaction, differenceText, differenceAmountText, coins, isChangeless);
			}
		}
	}

	private IEnumerable<PrivacyItem> VerifyLargePercentSpent(Parameters parameters)
	{
		// No need to check this warning if a Red Coin is selected.
		if(parameters.Transaction.SpentCoins.Any(x => x.GetPrivacyLevel(_wallet.AnonScoreTarget) == PrivacyLevel.NonPrivate))
		{
			yield break;
		}

		var spentAmount = parameters.Transaction.SpentCoins.Sum(x => x.Amount);
		var walletBalance = _wallet.Coins.TotalAmount().Satoshi;

		if (spentAmount <= LargePortionSpentMinAmount || spentAmount <= walletBalance * LargePortionSpentTolerance)
		{
			yield break;
		}

		var spentPercentage = (int)(spentAmount * 100m / walletBalance);
		yield return new LargePercentSpentWarning(spentPercentage);
	}

	private IEnumerable<PrivacyItem> VerifyUnconfirmedInputs(Parameters parameters)
	{
		if (parameters.Transaction.SpendsUnconfirmed)
		{
			yield return new UnconfirmedFundsWarning();
		}
	}

	private IEnumerable<PrivacyItem> VerifyCoinjoiningInputs(Parameters parameters)
	{
		if (parameters.Transaction.SpendsCoinjoining)
		{
			yield return new CoinjoiningFundsWarning();
		}
	}

	private async IAsyncEnumerable<PrivacyItem> VerifyChangeAsync(Parameters parameters, CancellationTokenSource linkedCts)
	{
		var destinationScriptPubKey = parameters.TransactionInfo.Destination.GetScriptPubKey();
		var hasChange = parameters.Transaction.InnerWalletOutputs.Any(x => x.ScriptPubKey != destinationScriptPubKey);

		if (hasChange)
		{
			yield return new CreatesChangeWarning();

			if (parameters.IncludeSuggestions && !parameters.TransactionInfo.IsFixedAmount && !parameters.TransactionInfo.IsPayJoin)
			{
				var suggestions = await CreateChangeAvoidanceSuggestionsAsync(parameters.TransactionInfo, parameters.Transaction, linkedCts).ConfigureAwait(false);
				foreach (var suggestion in suggestions)
				{
					yield return suggestion;
				}
			}
		}
	}

	private async Task<List<ChangeAvoidanceSuggestion>> CreateChangeAvoidanceSuggestionsAsync(TransactionInfo info, BuildTransactionResult transaction, CancellationTokenSource linkedCts)
	{
		var result = new List<ChangeAvoidanceSuggestion>();

		// Exchange rate can change substantially during computation itself.
		// Reporting up-to-date exchange rates would just confuse users.
		decimal usdExchangeRate = _exchangeRateUpdater.UsdExchangeRate;

		// Only allow to create 2 more inputs with BnB.
		int maxInputCount = transaction.SpentCoins.Count() + 2;

		var pockets = _sendFlow.GetPockets();
		var spentCoins = transaction.SpentCoins;
		var usedPockets = pockets.Where(x => x.Coins.Any(coin => spentCoins.Contains(coin)));
		ImmutableArray<SmartCoin> coinsToUse = usedPockets.SelectMany(x => x.Coins).ToImmutableArray();

		// If the original transaction couldn't avoid the CJing coins, BnB can use them too. Otherwise exclude them.
		var coinsInCoinJoin = _cjManager?.CoinsInCriticalPhase[_wallet.WalletId] ?? [];
		coinsToUse = spentCoins.Any(coinsInCoinJoin.Contains) ? coinsToUse : coinsToUse.Except(coinsInCoinJoin).ToImmutableArray();

		// If the original transaction is using only confirmed coins, BnB can use only them too. Otherwise let unconfirmed coins stay in the list.
		if (spentCoins.All(x => x.Confirmed))
		{
			coinsToUse = coinsToUse.Where(x => x.Confirmed).ToImmutableArray();
		}

		var suggestions = CreateChangeAvoidanceSuggestionsAsync(info, coinsToUse, maxInputCount, usdExchangeRate, linkedCts.Token).ConfigureAwait(false);

		await foreach (var suggestion in suggestions)
		{
			var changeAvoidanceSuggestions = result.ToArray();
			var newSuggestionAmount = suggestion.GetAmount(info.Destination);

			// If BnB solutions become the same transaction somehow, do not show the same suggestion twice.
			if (changeAvoidanceSuggestions.Any(x => x.GetAmount(info.Destination) == newSuggestionAmount))
			{
				continue;
			}

			// If BnB solution has the same amount as the original transaction, only suggest that one and stop searching.
			if (newSuggestionAmount == transaction.CalculateDestinationAmount(info.Destination))
			{
				result.RemoveMany(changeAvoidanceSuggestions);
				result.Add(suggestion);
				return result;
			}

			// If both is Less/More, only return the one with smaller difference.
			if (changeAvoidanceSuggestions.FirstOrDefault(x => x.IsLess == suggestion.IsLess && x.IsMore == suggestion.IsMore) is { } existingSuggestion)
			{
				if (Math.Abs(suggestion.Difference) < Math.Abs(existingSuggestion.Difference))
				{
					result.Remove(existingSuggestion);
					result.Add(suggestion);
				}
				return result;
			}

			result.Add(suggestion);
		}

		return result;
	}

	private async IAsyncEnumerable<ChangeAvoidanceSuggestion> CreateChangeAvoidanceSuggestionsAsync(
		TransactionInfo transactionInfo,
		ImmutableArray<SmartCoin> coinsToUse,
		int maxInputCount,
		decimal usdExchangeRate,
		[EnumeratorCancellation] CancellationToken cancellationToken)
	{
		var selectionsTask =
			ChangelessTransactionCoinSelector.GetAllStrategyResultsAsync(
				coinsToUse,
				transactionInfo.FeeRate,
				new TxOut(transactionInfo.Amount, transactionInfo.Destination.GetScriptPubKey()),
				maxInputCount,
				cancellationToken);

		await foreach (IReadOnlyList<SmartCoin> selection in selectionsTask.ConfigureAwait(false))
		{
			BuildTransactionResult? transaction = null;

			try
			{
				transaction = _wallet.BuildChangelessTransaction(
					transactionInfo.Destination,
					transactionInfo.Recipient,
					transactionInfo.FeeRate,
					selection,
					tryToSign: false);
			}
			catch (Exception ex)
			{
				Logger.LogError($"Failed to build changeless transaction. Exception: {ex}");
			}

			if (transaction is not null)
			{
				var (btcDifference, fiatDifference) = GetDifference(transactionInfo, transaction, usdExchangeRate);
				var differenceText = GetDifferenceText(btcDifference);
				var differenceAmountText = GetDifferenceAmountText(btcDifference, fiatDifference);
				var isMore = btcDifference > 0;
				var isLess = btcDifference < 0;

				yield return new ChangeAvoidanceSuggestion(transaction, fiatDifference, differenceText, differenceAmountText, isMore, isLess);
			}
		}
	}

	private bool TryCreateTransaction(
		TransactionInfo transactionInfo,
		SmartCoin[] coins,
		[NotNullWhen(true)] out BuildTransactionResult? txn,
		out bool isChangeless)
	{
		txn = null;
		isChangeless = false;

		try
		{
			// TODO: Verify the subtract fee change
			txn = _wallet.BuildTransaction(
				transactionInfo.Destination,
				transactionInfo.Amount,
				transactionInfo.Recipient,
				transactionInfo.FeeRate,
				coins,
				false,
				transactionInfo.PayJoinClient,
				tryToSign: false);
		}
		catch (Exception)
		{
			try
			{
				txn = _wallet.BuildChangelessTransaction(
					transactionInfo.Destination,
					transactionInfo.Recipient,
					transactionInfo.FeeRate,
					coins,
					tryToSign: false);

				isChangeless = true;
			}
			catch (Exception)
			{
				return false;
			}
		}

		return true;
	}

	private (decimal BtcDifference, decimal FiatDifference) GetDifference(TransactionInfo transactionInfo, BuildTransactionResult transaction, decimal usdExchangeRate)
	{
		var originalAmount = transactionInfo.Amount.ToDecimal(MoneyUnit.BTC);
		var totalAmount = transaction.CalculateDestinationAmount(transactionInfo.Destination);
		var total = totalAmount.ToDecimal(MoneyUnit.BTC);
		var btcDifference = total - originalAmount;
		var fiatTotal = total * usdExchangeRate;
		var fiatOriginal = originalAmount * usdExchangeRate;
		var fiatDifference = fiatTotal - fiatOriginal;

		return (btcDifference, fiatDifference);
	}

	private string GetDifferenceText(decimal btcDifference)
	{
		return btcDifference switch
		{
			> 0 => "more",
			< 0 => "less",
			_ => "the same amount"
		};
	}

	private string GetDifferenceAmountText(decimal btcDifference, decimal fiatDifference)
	{
		var absFiatDifference = Math.Abs(fiatDifference);
		return $"{Math.Abs(btcDifference).FormattedBtcFixedFractional()} BTC" +
			(absFiatDifference > 0
				? $" {Math.Abs(fiatDifference).ToUsdAproxBetweenParens()}"
				: "");
	}

	private record Parameters(TransactionInfo TransactionInfo, BuildTransactionResult Transaction, bool IncludeSuggestions);
}
