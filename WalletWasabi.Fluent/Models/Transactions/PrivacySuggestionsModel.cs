using DynamicData;
using NBitcoin;
using Nito.AsyncEx;
using System.Collections.Generic;
using System.Collections.Immutable;
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
	private CancellationTokenSource? _suggestionCancellationTokenSource;

	public PrivacySuggestionsModel(Wallet wallet)
	{
		_wallet = wallet;
	}

	/// <remarks>Method supports being called multiple times. In that case the last call cancels the previous one.</remarks>
	public async Task<PrivacySuggestionsResult> BuildPrivacySuggestionsAsync(TransactionInfo transaction, BuildTransactionResult transactionResult, CancellationToken cancellationToken)
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
				result = result.Combine(VerifyLabels(transactionResult))
							   .Combine(VerifyPrivacyLevel(transaction, transactionResult))
							   .Combine(VerifyConsolidation(transactionResult))
							   .Combine(VerifyUnconfirmedInputs(transactionResult))
							   .Combine(await VerifyChangeAsync(transaction, transactionResult, linkedCts));
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

	private PrivacySuggestionsResult VerifyLabels(BuildTransactionResult transactionResult)
	{
		var result = new PrivacySuggestionsResult();

		var coinLabels =
			transactionResult.SpentCoins
							 .SelectMany(x => x.GetLabels(_wallet.AnonScoreTarget))
							 .Distinct()
							 .ToList();

		var interlinkedLabels =
			transactionResult.InnerWalletOutputs
							 .Where(x => x.GetPrivacyLevel(_wallet) != PrivacyLevel.Private)
							 .Select(x => x.GetLabels(_wallet.AnonScoreTarget))
							 .Where(x => x.Any(l => coinLabels.Contains(l)))
							 .SelectMany(x => x)
							 .Distinct()
							 .Order()
							 .ToList();

		if (interlinkedLabels.Any())
		{
			result.Warnings.Add(new InterlinksLabelsWarning(new LabelsArray(interlinkedLabels)));
			result.Suggestions.Add(new LabelManagementSuggestion());
		}

		return result;
	}

	private PrivacySuggestionsResult VerifyPrivacyLevel(TransactionInfo transactionInfo, BuildTransactionResult originalTransaction)
	{
		var result = new PrivacySuggestionsResult();

		var nonPrivateCoins =
			originalTransaction.SpentCoins
							   .Where(x => x.GetPrivacyLevel(_wallet.AnonScoreTarget) == PrivacyLevel.NonPrivate)
							   .ToList();

		var semiPrivateCoins =
			originalTransaction.SpentCoins
							   .Where(x => x.GetPrivacyLevel(_wallet.AnonScoreTarget) == PrivacyLevel.SemiPrivate)
							   .ToList();

		if (!nonPrivateCoins.Any() && !semiPrivateCoins.Any())
		{
			return result;
		}

		var usdExchangeRate = _wallet.Synchronizer.UsdExchangeRate;

		if (nonPrivateCoins.Any())
		{
			result.Warnings.Add(new NonPrivateFundsWarning());
		}
		if (semiPrivateCoins.Any())
		{
			result.Warnings.Add(new SemiPrivateFundsWarning());
		}

		var totalAmount = originalTransaction.CalculateDestinationAmount().ToDecimal(MoneyUnit.BTC);

		var fullPrivacyCoinsToRemove = nonPrivateCoins.Concat(semiPrivateCoins).ToList();
		var fullPrivacyAmount = fullPrivacyCoinsToRemove.Sum(x => x.Amount.ToDecimal(MoneyUnit.BTC));

		if (fullPrivacyAmount <= (totalAmount * MaximumDifferenceTolerance))
		{
			var newTransaction = CreateTransaction(transactionInfo, new Money(fullPrivacyAmount, MoneyUnit.BTC), fullPrivacyCoinsToRemove);
			var differenceFiatText = GetDifferenceFiatText(transactionInfo, newTransaction, usdExchangeRate);
			result.Suggestions.Add(new FullPrivacySuggestion(newTransaction, differenceFiatText));
		}

		var betterPrivacyAmount =
			nonPrivateCoins.Sum(x => x.Amount.ToDecimal(MoneyUnit.BTC));

		if (betterPrivacyAmount <= (totalAmount * MaximumDifferenceTolerance))
		{
			var newTransaction = CreateTransaction(transactionInfo, new Money(betterPrivacyAmount, MoneyUnit.BTC), nonPrivateCoins);
			var differenceFiatText = GetDifferenceFiatText(transactionInfo, newTransaction, usdExchangeRate);
			result.Suggestions.Add(new BetterPrivacySuggestion(newTransaction, differenceFiatText));
		}

		return result;
	}

	private PrivacySuggestionsResult VerifyConsolidation(BuildTransactionResult originalTransaction)
	{
		var result = new PrivacySuggestionsResult();

		var consolidatedCoins = originalTransaction.SpentCoins.Count();

		if (consolidatedCoins >= ConsolidationTolerance)
		{
			result.Warnings.Add(new ConsolidationWarning(consolidatedCoins));
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

		if (hasChange && !info.IsFixedAmount && !info.IsPayJoin)
		{
			result.Warnings.Add(new CreatesChangeWarning());
			result.Suggestions.AddRange(await CreateChangeAvoidanceSuggestionsAsync(info, transaction, linkedCts));
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

		var pockets = _wallet.GetPockets();
		var spentCoins = transaction.SpentCoins;
		var usedPockets = pockets.Where(x => x.Coins.Any(coin => spentCoins.Contains(coin)));
		var coinsToUse = usedPockets.SelectMany(x => x.Coins).ToImmutableArray();

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

	private BuildTransactionResult CreateTransaction(TransactionInfo transactionInfo, Money? difference = null, IEnumerable<SmartCoin>? coinsToRemove = null, LabelsArray? labels = null)
	{
		var newAmount = transactionInfo.Amount - (difference ?? Money.Zero);
		var newCoins = transactionInfo.Coins.Except(coinsToRemove ?? Array.Empty<SmartCoin>()).ToList();
		var newLabels = labels ?? transactionInfo.Recipient;

		var transaction = TransactionHelpers.BuildTransaction(
			_wallet,
			transactionInfo.Destination,
			newAmount,
			newLabels,
			transactionInfo.FeeRate,
			newCoins,
			transactionInfo.SubtractFee,
			transactionInfo.PayJoinClient);
		return transaction;
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
			> 0 => $"{fiatDifference.ToUsd()} More",
			< 0 => $"{Math.Abs(fiatDifference).ToUsd()} Less",
			_ => "Same Amount"
		};

		return differenceFiatText;
	}
}
