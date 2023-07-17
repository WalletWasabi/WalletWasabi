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
using WalletWasabi.Logging;
using WalletWasabi.WabiSabi.Client;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.Models.Transactions;

public class PrivacySuggestionsModel
{
	private const decimal MaximumDifferenceTolerance = 0.25m;
	private const int ConsolidationTolerance = 10;

	/// <remarks>Guards use of <see cref="_suggestionCancellationTokenSource"/>.</remarks>
	private readonly object _lock = new();

	/// <summary>Allow at most one suggestion generation run.</summary>
	private readonly AsyncLock _asyncLock = new();

	private readonly Wallet _wallet;
	private readonly CoinJoinManager _cjManager;
	private CancellationTokenSource? _suggestionCancellationTokenSource;

	public PrivacySuggestionsModel(Wallet wallet)
	{
		_wallet = wallet;
		_cjManager = Services.HostedServices.Get<CoinJoinManager>();
	}

	/// <remarks>Method supports being called multiple times. In that case the last call cancels the previous one.</remarks>
	public async Task<PrivacySuggestionsResult> BuildPrivacySuggestionsAsync(TransactionInfo info, BuildTransactionResult transactionResult, CancellationToken cancellationToken)
	{
		var result = new PrivacySuggestionsResult();

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
				result = result
					.Combine(VerifyLabels(info, transactionResult))
					.Combine(VerifyPrivacyLevel(info, transactionResult))
					.Combine(VerifyConsolidation(transactionResult))
					.Combine(VerifyUnconfirmedInputs(transactionResult))
					.Combine(await VerifyChangeAsync(info, transactionResult, linkedCts));
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

		return result;
	}

	private PrivacySuggestionsResult VerifyLabels(TransactionInfo info, BuildTransactionResult transactionResult)
	{
		var result = new PrivacySuggestionsResult();

		var labels = transactionResult.SpentCoins.SelectMany(x => x.GetLabels(_wallet.AnonScoreTarget)).Except(info.Recipient);
		var labelsArray = new LabelsArray(labels);

		if (labelsArray.Any())
		{
			result.Warnings.Add(new InterlinksLabelsWarning(labelsArray));

			if (info.IsOtherPocketSelectionPossible)
			{
				result.Suggestions.Add(new LabelManagementSuggestion());
			}
		}

		return result;
	}

	private PrivacySuggestionsResult VerifyPrivacyLevel(TransactionInfo transactionInfo, BuildTransactionResult originalTransaction)
	{
		var result = new PrivacySuggestionsResult();

		var canModifyTransactionAmount = !transactionInfo.IsPayJoin && !transactionInfo.IsFixedAmount;

		var transactionLabels = originalTransaction.SpentCoins.SelectMany(x => x.GetLabels(_wallet.AnonScoreTarget));
		var onlyKnownByRecipient =
			transactionInfo.Recipient.Equals(new LabelsArray(transactionLabels), StringComparer.OrdinalIgnoreCase);

		var foundNonPrivate = !onlyKnownByRecipient &&
							  originalTransaction.SpentCoins.Any(x => x.GetPrivacyLevel(_wallet.AnonScoreTarget) == PrivacyLevel.NonPrivate);

		var foundSemiPrivate =
			originalTransaction.SpentCoins.Any(x => x.GetPrivacyLevel(_wallet.AnonScoreTarget) == PrivacyLevel.SemiPrivate);

		if (foundNonPrivate)
		{
			result.Warnings.Add(new NonPrivateFundsWarning());
		}

		if (foundSemiPrivate)
		{
			result.Warnings.Add(new SemiPrivateFundsWarning());
		}

		ImmutableList<SmartCoin> coinsToExclude = _cjManager.CoinsInCriticalPhase[_wallet.WalletName];

		bool wasCoinjoiningCoinUsed = originalTransaction.SpentCoins.Any(coinsToExclude.Contains);

		// Only exclude coins if the original transaction doesn't use them either.
		var allPrivateCoin = wasCoinjoiningCoinUsed ?
			_wallet.Coins.Where(x => x.GetPrivacyLevel(_wallet.AnonScoreTarget) == PrivacyLevel.Private).ToArray() :
			_wallet.Coins.Where(x => x.GetPrivacyLevel(_wallet.AnonScoreTarget) == PrivacyLevel.Private).Except(coinsToExclude).ToArray();

		var onlyKnownByTheRecipientCoins = wasCoinjoiningCoinUsed ?
			_wallet.Coins.Where(x => transactionInfo.Recipient.Equals(x.GetLabels(_wallet.AnonScoreTarget), StringComparer.OrdinalIgnoreCase)).ToArray() :
			_wallet.Coins.Where(x => transactionInfo.Recipient.Equals(x.GetLabels(_wallet.AnonScoreTarget), StringComparer.OrdinalIgnoreCase)).Except(coinsToExclude).ToArray();

		var allSemiPrivateCoin = wasCoinjoiningCoinUsed ?
			_wallet.Coins.Where(x => x.GetPrivacyLevel(_wallet.AnonScoreTarget) == PrivacyLevel.SemiPrivate)
			.Union(onlyKnownByTheRecipientCoins)
			.ToArray()
			:
			_wallet.Coins.Where(x => x.GetPrivacyLevel(_wallet.AnonScoreTarget) == PrivacyLevel.SemiPrivate)
			.Union(onlyKnownByTheRecipientCoins)
			.Except(coinsToExclude)
			.ToArray();

		var usdExchangeRate = _wallet.Synchronizer.UsdExchangeRate;
		var totalAmount = originalTransaction.CalculateDestinationAmount().ToDecimal(MoneyUnit.BTC);
		FullPrivacySuggestion? fullPrivacySuggestion = null;

		if ((foundNonPrivate || foundSemiPrivate) && allPrivateCoin.Any() &&
			TryCreateTransaction(transactionInfo, allPrivateCoin, out var newTransaction, out var isChangeless))
		{
			var amountDifference = totalAmount - newTransaction.CalculateDestinationAmount().ToDecimal(MoneyUnit.BTC);
			var amountDifferencePercentage = amountDifference / totalAmount;

			if (amountDifferencePercentage <= MaximumDifferenceTolerance && (canModifyTransactionAmount || amountDifference == 0m))
			{
				var differenceFiatText = GetDifferenceFiatText(transactionInfo, newTransaction, usdExchangeRate);
				fullPrivacySuggestion = new FullPrivacySuggestion(newTransaction, amountDifference, differenceFiatText, allPrivateCoin, isChangeless);
				result.Suggestions.Add(fullPrivacySuggestion);
			}
		}

		// Do not calculate the better privacy option when the full privacy option has the same amount.
		// This is only possible if the user makes a silly selection with coin control.
		if (fullPrivacySuggestion is { } sug && sug.Difference == 0m)
		{
			return result;
		}

		var coins = allPrivateCoin.Union(allSemiPrivateCoin).ToArray();
		if (foundNonPrivate && allSemiPrivateCoin.Any() &&
			TryCreateTransaction(transactionInfo, coins, out newTransaction, out isChangeless))
		{
			var amountDifference = totalAmount - newTransaction.CalculateDestinationAmount().ToDecimal(MoneyUnit.BTC);
			var amountDifferencePercentage = amountDifference / totalAmount;

			if (amountDifferencePercentage <= MaximumDifferenceTolerance && (canModifyTransactionAmount || amountDifference == 0m))
			{
				var differenceFiatText = GetDifferenceFiatText(transactionInfo, newTransaction, usdExchangeRate);
				result.Suggestions.Add(new BetterPrivacySuggestion(newTransaction, differenceFiatText, coins, isChangeless));
			}
		}

		return result;
	}

	private PrivacySuggestionsResult VerifyConsolidation(BuildTransactionResult originalTransaction)
	{
		var result = new PrivacySuggestionsResult();

		var consolidatedCoins = originalTransaction.SpentCoins.Count();

		if (consolidatedCoins > ConsolidationTolerance)
		{
			result.Warnings.Add(new ConsolidationWarning(ConsolidationTolerance));
		}

		return result;
	}

	private PrivacySuggestionsResult VerifyUnconfirmedInputs(BuildTransactionResult transaction)
	{
		var result = new PrivacySuggestionsResult();

		if (transaction.SpendsUnconfirmed)
		{
			result.Warnings.Add(new UnconfirmedFundsWarning());
		}

		return result;
	}

	private async Task<PrivacySuggestionsResult> VerifyChangeAsync(TransactionInfo info, BuildTransactionResult transaction, CancellationTokenSource linkedCts)
	{
		var result = new PrivacySuggestionsResult();

		var hasChange = transaction.InnerWalletOutputs.Any(x => x.ScriptPubKey != info.Destination.ScriptPubKey);

		if (hasChange)
		{
			result.Warnings.Add(new CreatesChangeWarning());

			if (!info.IsFixedAmount && !info.IsPayJoin)
			{
				result.Suggestions.AddRange(await CreateChangeAvoidanceSuggestionsAsync(info, transaction, linkedCts));
			}
		}

		return result;
	}

	private async Task<List<ChangeAvoidanceSuggestion>> CreateChangeAvoidanceSuggestionsAsync(TransactionInfo info, BuildTransactionResult transaction, CancellationTokenSource linkedCts)
	{
		var result = new List<ChangeAvoidanceSuggestion>();

		// Exchange rate can change substantially during computation itself.
		// Reporting up-to-date exchange rates would just confuse users.
		decimal usdExchangeRate = _wallet.Synchronizer.UsdExchangeRate;

		// Only allow to create 1 more input with BnB. This accounts for the change created.
		int maxInputCount = transaction.SpentCoins.Count() + 1;

		ImmutableList<SmartCoin> coinsToExclude = _cjManager.CoinsInCriticalPhase[_wallet.WalletName];

		var pockets = _wallet.GetPockets();
		var spentCoins = transaction.SpentCoins;
		var usedPockets = pockets.Where(x => x.Coins.Any(coin => spentCoins.Contains(coin)));

		// If the original transaction couldn't avoid the CJing coins, BnB can use them too. Otherwise exclude them.
		ImmutableArray<SmartCoin> coinsToUse = spentCoins.Any(coinsToExclude.Contains) ?
			usedPockets.SelectMany(x => x.Coins).ToImmutableArray() :
			usedPockets.SelectMany(x => x.Coins).Except(coinsToExclude).ToImmutableArray();

		var suggestions = CreateChangeAvoidanceSuggestionsAsync(info, coinsToUse, maxInputCount, usdExchangeRate, linkedCts.Token);

		await foreach (var suggestion in suggestions)
		{
			var changeAvoidanceSuggestions = result.ToArray();
			var newSuggestionAmount = suggestion.GetAmount();

			// If BnB solutions become the same transaction somehow, do not show the same suggestion twice.
			if (changeAvoidanceSuggestions.Any(x => x.GetAmount() == newSuggestionAmount))
			{
				continue;
			}

			// If BnB solution has the same amount as the original transaction, only suggest that one and stop searching.
			if (newSuggestionAmount == transaction.CalculateDestinationAmount())
			{
				result.RemoveMany(changeAvoidanceSuggestions);
				result.Add(suggestion);
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
				new TxOut(transactionInfo.Amount, transactionInfo.Destination),
				maxInputCount,
				cancellationToken);

		await foreach (IEnumerable<SmartCoin> selection in selectionsTask.ConfigureAwait(false))
		{
			BuildTransactionResult? transaction = null;

			try
			{
				transaction = TransactionHelpers.BuildChangelessTransaction(
					_wallet,
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
				yield return new ChangeAvoidanceSuggestion(transaction, GetDifferenceFiatText(transactionInfo, transaction, usdExchangeRate));
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
			txn = TransactionHelpers.BuildTransaction(
				_wallet,
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
				txn = TransactionHelpers.BuildChangelessTransaction(
					_wallet,
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

	private string GetDifferenceFiatText(TransactionInfo transactionInfo, BuildTransactionResult transaction, decimal usdExchangeRate)
	{
		var originalAmount = transactionInfo.Amount.ToDecimal(MoneyUnit.BTC);
		var totalAmount = transaction.CalculateDestinationAmount();
		var total = totalAmount.ToDecimal(MoneyUnit.BTC);
		var fiatTotal = total * usdExchangeRate;
		var fiatOriginal = originalAmount * usdExchangeRate;
		var fiatDifference = fiatTotal - fiatOriginal;

		var differenceFiatText = fiatDifference switch
		{
			> 0 => $"{fiatDifference.ToUsd()} more",
			< 0 => $"{Math.Abs(fiatDifference).ToUsd()} less",
			_ => "the same amount"
		};

		return differenceFiatText;
	}
}
