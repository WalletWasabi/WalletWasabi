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

	/// <remarks>Guards use of <see cref="_singleRunCancellationTokenSource"/>.</remarks>
	private readonly object _lock = new();

	/// <summary>Allow at most one suggestion generation run.</summary>
	private readonly AsyncLock _asyncLock = new();

	private readonly Wallet _wallet;
	private readonly CoinJoinManager _cjManager;

	private CancellationTokenSource? _singleRunCancellationTokenSource;
	private CancellationTokenSource? _linkedCancellationTokenSource;

	public PrivacySuggestionsModel(Wallet wallet)
	{
		_wallet = wallet;
		_cjManager = Services.HostedServices.Get<CoinJoinManager>();
	}

	/// <remarks>Method supports being called multiple times. In that case the last call cancels the previous one.</remarks>
	public async IAsyncEnumerable<PrivacyItem> BuildPrivacySuggestionsAsync(TransactionInfo info, BuildTransactionResult transactionResult, [EnumeratorCancellation] CancellationToken cancellationToken)
	{
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
			result.Add(VerifyLabels(info, transactionResult));
			result.Add(VerifyPrivacyLevel(info, transactionResult));
			result.Add(VerifyConsolidation(transactionResult));
			result.Add(VerifyUnconfirmedInputs(transactionResult));
			result.Add(VerifyCoinjoiningInputs(transactionResult));
			foreach (var item in result)
			{
				yield return item;
			}
			await foreach (var item in VerifyChangeAsync(info, transactionResult, _linkedCancellationTokenSource).ConfigureAwait(false))
			{
				yield return item;
			}
			lock (_lock)
			{
				_singleRunCancellationTokenSource = null;
			}
		}
	}

	private IEnumerable<PrivacyItem> VerifyLabels(TransactionInfo info, BuildTransactionResult transactionResult)
	{
		var warning = GetLabelWarning(transactionResult, info.Recipient);

		if (warning is not null)
		{
			yield return warning;

			if (info.IsOtherPocketSelectionPossible)
			{
				yield return new LabelManagementSuggestion();
			}
		}
	}

	private PrivacyItem? GetLabelWarning(BuildTransactionResult transactionResult, LabelsArray recipient)
	{
		var pockets = _wallet.GetPockets();
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

	private IEnumerable<PrivacyItem> VerifyPrivacyLevel(TransactionInfo transactionInfo, BuildTransactionResult originalTransaction)
	{
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
			yield return new NonPrivateFundsWarning();
		}

		if (foundSemiPrivate)
		{
			yield return new SemiPrivateFundsWarning();
		}

		ImmutableList<SmartCoin> coinsToExclude = _cjManager.CoinsInCriticalPhase[_wallet.WalletId];
		bool wasCoinjoiningCoinUsed = originalTransaction.SpentCoins.Any(coinsToExclude.Contains);

		// Only exclude coins if the original transaction doesn't use them either.
		var allPrivateCoin = _wallet.Coins.Where(x => x.GetPrivacyLevel(_wallet.AnonScoreTarget) == PrivacyLevel.Private).ToArray();

		allPrivateCoin = wasCoinjoiningCoinUsed ? allPrivateCoin : allPrivateCoin.Except(coinsToExclude).ToArray();

		var onlyKnownByTheRecipientCoins = _wallet.Coins.Where(x => transactionInfo.Recipient.Equals(x.GetLabels(_wallet.AnonScoreTarget), StringComparer.OrdinalIgnoreCase)).ToArray();
		var allSemiPrivateCoin =
			_wallet.Coins.Where(x => x.GetPrivacyLevel(_wallet.AnonScoreTarget) == PrivacyLevel.SemiPrivate)
			.Union(onlyKnownByTheRecipientCoins)
			.ToArray();

		allSemiPrivateCoin = wasCoinjoiningCoinUsed ? allSemiPrivateCoin : allSemiPrivateCoin.Except(coinsToExclude).ToArray();

		var usdExchangeRate = _wallet.Synchronizer.UsdExchangeRate;
		var totalAmount = originalTransaction.CalculateDestinationAmount(transactionInfo.Destination).ToDecimal(MoneyUnit.BTC);
		FullPrivacySuggestion? fullPrivacySuggestion = null;

		if ((foundNonPrivate || foundSemiPrivate) && allPrivateCoin.Any() &&
			TryCreateTransaction(transactionInfo, allPrivateCoin, out var newTransaction, out var isChangeless))
		{
			var amountDifference = totalAmount - newTransaction.CalculateDestinationAmount(transactionInfo.Destination).ToDecimal(MoneyUnit.BTC);
			var amountDifferencePercentage = amountDifference / totalAmount;

			if (amountDifferencePercentage <= MaximumDifferenceTolerance && (canModifyTransactionAmount || amountDifference == 0m))
			{
				var differenceFiat = GetDifferenceFiat(transactionInfo, newTransaction, usdExchangeRate);
				var differenceFiatText = GetDifferenceFiatText(differenceFiat);
				fullPrivacySuggestion = new FullPrivacySuggestion(newTransaction, amountDifference, differenceFiatText, allPrivateCoin, isChangeless);
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
		if (foundNonPrivate && allSemiPrivateCoin.Any() &&
			TryCreateTransaction(transactionInfo, coins, out newTransaction, out isChangeless))
		{
			var amountDifference = totalAmount - newTransaction.CalculateDestinationAmount(transactionInfo.Destination).ToDecimal(MoneyUnit.BTC);
			var amountDifferencePercentage = amountDifference / totalAmount;

			if (amountDifferencePercentage <= MaximumDifferenceTolerance && (canModifyTransactionAmount || amountDifference == 0m))
			{
				var differenceFiat = GetDifferenceFiat(transactionInfo, newTransaction, usdExchangeRate);
				var differenceFiatText = GetDifferenceFiatText(differenceFiat);
				yield return new BetterPrivacySuggestion(newTransaction, differenceFiatText, coins, isChangeless);
			}
		}
	}

	private IEnumerable<PrivacyItem> VerifyConsolidation(BuildTransactionResult originalTransaction)
	{
		var consolidatedCoins = originalTransaction.SpentCoins.Count();

		if (consolidatedCoins > ConsolidationTolerance)
		{
			yield return new ConsolidationWarning(ConsolidationTolerance);
		}
	}

	private IEnumerable<PrivacyItem> VerifyUnconfirmedInputs(BuildTransactionResult transaction)
	{
		if (transaction.SpendsUnconfirmed)
		{
			yield return new UnconfirmedFundsWarning();
		}
	}

	private IEnumerable<PrivacyItem> VerifyCoinjoiningInputs(BuildTransactionResult transaction)
	{
		if (transaction.SpendsCoinjoining)
		{
			yield return new CoinjoiningFundsWarning();
		}
	}

	private async IAsyncEnumerable<PrivacyItem> VerifyChangeAsync(TransactionInfo info, BuildTransactionResult transaction, CancellationTokenSource linkedCts)
	{
		var hasChange = transaction.InnerWalletOutputs.Any(x => x.ScriptPubKey != info.Destination.ScriptPubKey);

		if (hasChange)
		{
			yield return new CreatesChangeWarning();

			if (!info.IsFixedAmount && !info.IsPayJoin)
			{
				var suggestions = await CreateChangeAvoidanceSuggestionsAsync(info, transaction, linkedCts).ConfigureAwait(false);
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
		decimal usdExchangeRate = _wallet.Synchronizer.UsdExchangeRate;

		// Only allow to create 1 more input with BnB. This accounts for the change created.
		int maxInputCount = transaction.SpentCoins.Count() + 1;

		ImmutableList<SmartCoin> coinsToExclude = _cjManager.CoinsInCriticalPhase[_wallet.WalletId];

		var pockets = _wallet.GetPockets();
		var spentCoins = transaction.SpentCoins;
		var usedPockets = pockets.Where(x => x.Coins.Any(coin => spentCoins.Contains(coin)));
		ImmutableArray<SmartCoin> coinsToUse = usedPockets.SelectMany(x => x.Coins).ToImmutableArray();

		// If the original transaction couldn't avoid the CJing coins, BnB can use them too. Otherwise exclude them.
		coinsToUse = spentCoins.Any(coinsToExclude.Contains) ? coinsToUse : coinsToUse.Except(coinsToExclude).ToImmutableArray();

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
				new TxOut(transactionInfo.Amount, transactionInfo.Destination),
				maxInputCount,
				cancellationToken);

		await foreach (IEnumerable<SmartCoin> selection in selectionsTask.ConfigureAwait(false))
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
				var differenceFiat = GetDifferenceFiat(transactionInfo, transaction, usdExchangeRate);
				yield return new ChangeAvoidanceSuggestion(transaction, differenceFiat, GetDifferenceFiatText(differenceFiat), IsMore: differenceFiat > 0, IsLess: differenceFiat < 0);
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

	private decimal GetDifferenceFiat(TransactionInfo transactionInfo, BuildTransactionResult transaction, decimal usdExchangeRate)
	{
		var originalAmount = transactionInfo.Amount.ToDecimal(MoneyUnit.BTC);
		var totalAmount = transaction.CalculateDestinationAmount(transactionInfo.Destination);
		var total = totalAmount.ToDecimal(MoneyUnit.BTC);
		var fiatTotal = total * usdExchangeRate;
		var fiatOriginal = originalAmount * usdExchangeRate;
		var fiatDifference = fiatTotal - fiatOriginal;

		return fiatDifference;
	}

	private string GetDifferenceFiatText(decimal fiatDifference)
	{
		return fiatDifference switch
		{
			> 0 => $"{fiatDifference.ToUsd()} more",
			< 0 => $"{Math.Abs(fiatDifference).ToUsd()} less",
			_ => "the same amount"
		};
	}
}
