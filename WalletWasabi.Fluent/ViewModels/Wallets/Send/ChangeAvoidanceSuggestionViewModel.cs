using NBitcoin;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Blockchain.TransactionBuilding;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Send;

public partial class ChangeAvoidanceSuggestionViewModel : SuggestionViewModel
{
	[AutoNotify] private string _amount;
	[AutoNotify] private string _amountFiat;
	[AutoNotify] private string? _differenceFiat;

	public ChangeAvoidanceSuggestionViewModel(
		decimal originalAmount,
		BuildTransactionResult transactionResult,
		decimal fiatExchangeRate)
	{
		TransactionResult = transactionResult;

		decimal total = transactionResult.CalculateDestinationAmount().ToDecimal(MoneyUnit.BTC);

		_amountFiat = total.GenerateFiatText(fiatExchangeRate, "USD");

		var fiatTotal = total * fiatExchangeRate;
		var fiatOriginal = originalAmount * fiatExchangeRate;
		var fiatDifference = fiatTotal - fiatOriginal;

		_differenceFiat = (fiatDifference > 0
				? $"{fiatDifference.GenerateFiatText("USD")} More"
				: $"{Math.Abs(fiatDifference).GenerateFiatText("USD")} Less")
			.Replace("(", "").Replace(")", "");

		_amount = $"{total} BTC";
	}

	public BuildTransactionResult TransactionResult { get; }

	public static async IAsyncEnumerable<ChangeAvoidanceSuggestionViewModel> GenerateSuggestionsAsync(
		TransactionInfo transactionInfo,
		BitcoinAddress destination,
		Wallet wallet,
		int maxInputCount,
		decimal usdExchangeRate,
		[EnumeratorCancellation] CancellationToken cancellationToken)
	{
		var selections = ChangelessTransactionCoinSelector.GetAllStrategyResultsAsync(
			transactionInfo.Coins,
			transactionInfo.FeeRate,
			new TxOut(transactionInfo.Amount, destination),
			maxInputCount,
			cancellationToken).ConfigureAwait(false);

		HashSet<Money> foundSolutionsByAmount = new();

		await foreach (var selection in selections)
		{
			if (selection.Any())
			{
				BuildTransactionResult transaction = TransactionHelpers.BuildChangelessTransaction(
					wallet,
					destination,
					transactionInfo.UserLabels,
					transactionInfo.FeeRate,
					selection,
					tryToSign: false);

				var destinationAmount = transaction.CalculateDestinationAmount();

				// If Bnb solutions become the same transaction somehow, do not show the same suggestion twice.
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
