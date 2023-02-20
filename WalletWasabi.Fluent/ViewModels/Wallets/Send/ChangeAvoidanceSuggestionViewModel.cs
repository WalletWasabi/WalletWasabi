using NBitcoin;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Blockchain.TransactionBuilding;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Logging;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Send;

public partial class ChangeAvoidanceSuggestionViewModel : SuggestionViewModel
{
	[AutoNotify] private string _amount;
	[AutoNotify] private decimal _amountFiat;
	[AutoNotify] private string? _differenceFiat;

	public ChangeAvoidanceSuggestionViewModel(
		decimal originalAmount,
		BuildTransactionResult transactionResult,
		decimal fiatExchangeRate)
	{
		TransactionResult = transactionResult;

		var totalAmount = transactionResult.CalculateDestinationAmount();
		var total = totalAmount.ToDecimal(MoneyUnit.BTC);

		var fiatTotal = total * fiatExchangeRate;

		_amountFiat = fiatTotal;

		var fiatOriginal = originalAmount * fiatExchangeRate;
		var fiatDifference = fiatTotal - fiatOriginal;

		_differenceFiat = (fiatDifference > 0
				? $"{fiatDifference.ToUsd()} More"
				: $"{Math.Abs(fiatDifference).ToUsd()} Less")
			.Replace("(", "").Replace(")", "");

		_amount = $"{totalAmount.ToFormattedString()} BTC";
	}

	public BuildTransactionResult TransactionResult { get; }

	public static async IAsyncEnumerable<ChangeAvoidanceSuggestionViewModel> GenerateSuggestionsAsync(
		TransactionInfo transactionInfo,
		Wallet wallet,
		ImmutableArray<SmartCoin> coinsToUse,
		int maxInputCount,
		decimal usdExchangeRate,
		[EnumeratorCancellation] CancellationToken cancellationToken)
	{
		IAsyncEnumerable<IEnumerable<SmartCoin>> selectionsTask = ChangelessTransactionCoinSelector.GetAllStrategyResultsAsync(
			coinsToUse,
			transactionInfo.FeeRate,
			new TxOut(transactionInfo.Amount, transactionInfo.Destination),
			maxInputCount,
			cancellationToken);

		HashSet<Money> foundSolutionsByAmount = new();

		await foreach (IEnumerable<SmartCoin> selection in selectionsTask.ConfigureAwait(false))
		{
			if (selection.Any())
			{
				BuildTransactionResult? transaction = null;

				try
				{
					transaction = TransactionHelpers.BuildChangelessTransaction(
						wallet,
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
					Money destinationAmount = transaction.CalculateDestinationAmount();

					// If BnB solutions become the same transaction somehow, do not show the same suggestion twice.
					if (!foundSolutionsByAmount.Contains(destinationAmount))
					{
						foundSolutionsByAmount.Add(destinationAmount);

						yield return new ChangeAvoidanceSuggestionViewModel(
							transactionInfo.Amount.ToDecimal(MoneyUnit.BTC),
							transaction,
							usdExchangeRate);
					}
				}
			}
		}
	}
}
