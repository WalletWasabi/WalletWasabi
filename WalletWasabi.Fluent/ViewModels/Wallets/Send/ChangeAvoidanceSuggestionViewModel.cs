using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NBitcoin;
using WalletWasabi.Blockchain.TransactionBuilding;
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

	private static IEnumerable<ChangeAvoidanceSuggestionViewModel> NormalizeSuggestions(
		IEnumerable<ChangeAvoidanceSuggestionViewModel> suggestions,
		ChangeAvoidanceSuggestionViewModel defaultSuggestion)
	{
		var normalized = suggestions
			.OrderBy(x => x.TransactionResult.CalculateDestinationAmount())
			.ToList();

		if (normalized.Count == 3)
		{
			var index = normalized.IndexOf(defaultSuggestion);

			switch (index)
			{
				case 1:
					break;

				case 0:
					normalized = normalized.Take(2).ToList();
					break;

				case 2:
					normalized = normalized.Skip(1).ToList();
					break;
			}
		}

		return normalized;
	}

	public static async Task<IEnumerable<ChangeAvoidanceSuggestionViewModel>> GenerateSuggestionsAsync(
		TransactionInfo transactionInfo, BitcoinAddress destination, Wallet wallet, BuildTransactionResult requestedTransaction)
	{
		var intent = new PaymentIntent(
			destination,
			MoneyRequest.CreateAllRemaining(subtractFee: true),
			transactionInfo.UserLabels);

		ChangeAvoidanceSuggestionViewModel? smallerSuggestion = null;

		if (requestedTransaction.SpentCoins.Count() > 1)
		{
			var smallerTransaction = await Task.Run(() => wallet.BuildTransaction(
				wallet.Kitchen.SaltSoup(),
				intent,
				FeeStrategy.CreateFromFeeRate(transactionInfo.FeeRate),
				allowUnconfirmed: true,
				requestedTransaction
					.SpentCoins
					.OrderBy(x => x.Amount)
					.Skip(1)
					.Select(x => x.OutPoint)));

			smallerSuggestion = new ChangeAvoidanceSuggestionViewModel(
				transactionInfo.Amount.ToDecimal(MoneyUnit.BTC), smallerTransaction,
				wallet.Synchronizer.UsdExchangeRate, false);
		}

		var defaultSelection = new ChangeAvoidanceSuggestionViewModel(
			transactionInfo.Amount.ToDecimal(MoneyUnit.BTC), requestedTransaction,
			wallet.Synchronizer.UsdExchangeRate, true);

		var largerTransaction = await Task.Run(() => wallet.BuildTransaction(
			wallet.Kitchen.SaltSoup(),
			intent,
			FeeStrategy.CreateFromFeeRate(transactionInfo.FeeRate),
			true,
			requestedTransaction.SpentCoins.Select(x => x.OutPoint)));

		var largerSuggestion = new ChangeAvoidanceSuggestionViewModel(
			transactionInfo.Amount.ToDecimal(MoneyUnit.BTC), largerTransaction,
			wallet.Synchronizer.UsdExchangeRate, false);

		// There are several scenarios, both the alternate suggestions are <, or >, or 1 < and 1 >.
		// We sort them and add the suggestions accordingly.
		var suggestions = new List<ChangeAvoidanceSuggestionViewModel> { defaultSelection, largerSuggestion };

		if (smallerSuggestion is { })
		{
			suggestions.Add(smallerSuggestion);
		}

		var results = new List<ChangeAvoidanceSuggestionViewModel>();

		foreach (var suggestion in NormalizeSuggestions(suggestions, defaultSelection)
			         .Where(x => x != defaultSelection))
		{
			results.Add(suggestion);
		}

		return results;
	}
}