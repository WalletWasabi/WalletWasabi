using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using NBitcoin;
using WalletWasabi.Blockchain.TransactionBuilding;
using WalletWasabi.Blockchain.TransactionBuilding.BnB;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Send;

public partial class ChangeAvoidanceSuggestionViewModel : SuggestionViewModel
{
	[AutoNotify] private string _amount;
	[AutoNotify] private string _amountFiat;
	[AutoNotify] private string? _differenceFiat;

	public ChangeAvoidanceSuggestionViewModel(decimal originalAmount,
		BuildTransactionResult transactionResult,
		decimal fiatExchangeRate,
		bool isOriginal)
	{
		TransactionResult = transactionResult;

		decimal total = transactionResult.CalculateDestinationAmount().ToDecimal(MoneyUnit.BTC);

		_amountFiat = total.GenerateFiatText(fiatExchangeRate, "USD");

		if (!isOriginal)
		{
			var fiatTotal = total * fiatExchangeRate;
			var fiatOriginal = originalAmount * fiatExchangeRate;
			var fiatDifference = fiatTotal - fiatOriginal;

			_differenceFiat = (fiatDifference > 0
					? $"{fiatDifference.GenerateFiatText("USD")} More"
					: $"{Math.Abs(fiatDifference).GenerateFiatText("USD")} Less")
				.Replace("(", "").Replace(")", "");
		}

		_amount = $"{total} BTC";
	}

	public BuildTransactionResult TransactionResult { get; }

	public static async IAsyncEnumerable<ChangeAvoidanceSuggestionViewModel> GenerateSuggestionsAsync(
		TransactionInfo transactionInfo,
		BitcoinAddress destination,
		Wallet wallet,
		[EnumeratorCancellation] CancellationToken cancellationToken)
	{
		var selections = ChangelessTransactionCoinSelector.GetAllStrategyResultsAsync(
			transactionInfo.Coins,
			transactionInfo.FeeRate,
			new TxOut(transactionInfo.Amount, destination),
			cancellationToken).ConfigureAwait(false);

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

				yield return new ChangeAvoidanceSuggestionViewModel(
					transactionInfo.Amount.ToDecimal(MoneyUnit.BTC),
					transaction,
					wallet.Synchronizer.UsdExchangeRate,
					isOriginal: false);
			}
		}
	}
}
