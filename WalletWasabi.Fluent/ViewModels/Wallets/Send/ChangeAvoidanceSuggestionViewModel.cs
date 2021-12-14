using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using JetBrains.Annotations;
using NBitcoin;
using WalletWasabi.Blockchain.TransactionBuilding;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.Models;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Send;

public partial class ChangeAvoidanceSuggestionViewModel : SuggestionViewModel
{
	[AutoNotify] private string _amount;
	[AutoNotify] private string _amountFiat;
	[AutoNotify] private List<PrivacySuggestionBenefit> _benefits;
	[AutoNotify] private PrivacyOptimisationLevel _optimisationLevel;
	[AutoNotify] private bool _optimisationLevelGood;

	public ChangeAvoidanceSuggestionViewModel(decimal originalAmount,
		BuildTransactionResult transactionResult,
		PrivacyOptimisationLevel optimisationLevel,
		decimal fiatExchangeRate,
		bool isOriginal,
		params PrivacySuggestionBenefit[] benefits) : base(transactionResult, isOriginal)
	{
		_optimisationLevel = optimisationLevel;
		_benefits = benefits.ToList();

		decimal total = transactionResult.CalculateDestinationAmount().ToDecimal(MoneyUnit.BTC);

		_amountFiat = total.GenerateFiatText(fiatExchangeRate, "USD");
		_optimisationLevelGood = optimisationLevel == PrivacyOptimisationLevel.Better;

		if (_optimisationLevelGood)
		{
			var fiatTotal = total * fiatExchangeRate;
			var fiatOriginal = originalAmount * fiatExchangeRate;
			var fiatDifference = fiatTotal - fiatOriginal;

			var difference = (fiatDifference > 0
					? $"{fiatDifference.GenerateFiatText("USD")} More"
					: $"{Math.Abs(fiatDifference).GenerateFiatText("USD")} Less")
				.Replace("(", "").Replace(")", "");

			_benefits.Add(new PrivacySuggestionBenefit(false, difference));
		}
		else
		{
			// This is just to pad the control.
			_benefits.Add(new PrivacySuggestionBenefit(false, " "));
		}

		_amount = $"{total}";
	}

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
			TransactionInfo transactionInfo, Wallet wallet, BuildTransactionResult requestedTransaction)
	{
		var intent = new PaymentIntent(
			transactionInfo.Address,
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
				PrivacyOptimisationLevel.Better, wallet.Synchronizer.UsdExchangeRate, false,
				new PrivacySuggestionBenefit(true, "Improved Privacy"),
				new PrivacySuggestionBenefit(false, "No change, less trace"));
		}

		var defaultSelection = new ChangeAvoidanceSuggestionViewModel(
			transactionInfo.Amount.ToDecimal(MoneyUnit.BTC), requestedTransaction,
			PrivacyOptimisationLevel.Standard, wallet.Synchronizer.UsdExchangeRate, true,
			new PrivacySuggestionBenefit(false, "As Requested"));

		var largerTransaction = await Task.Run(() => wallet.BuildTransaction(
			wallet.Kitchen.SaltSoup(),
			intent,
			FeeStrategy.CreateFromFeeRate(transactionInfo.FeeRate),
			true,
			requestedTransaction.SpentCoins.Select(x => x.OutPoint)));

		var largerSuggestion = new ChangeAvoidanceSuggestionViewModel(
			transactionInfo.Amount.ToDecimal(MoneyUnit.BTC), largerTransaction,
			PrivacyOptimisationLevel.Better, wallet.Synchronizer.UsdExchangeRate, false,
			new PrivacySuggestionBenefit(true, "Improved Privacy"),
			new PrivacySuggestionBenefit(false, "No change, less trace"));

		// There are several scenarios, both the alternate suggestions are <, or >, or 1 < and 1 >.
		// We sort them and add the suggestions accordingly.
		var suggestions = new List<ChangeAvoidanceSuggestionViewModel> {defaultSelection, largerSuggestion};

		if (smallerSuggestion is { })
		{
			suggestions.Add(smallerSuggestion);
		}

		var results = new List<ChangeAvoidanceSuggestionViewModel>();

		foreach (var suggestion in NormalizeSuggestions(suggestions, defaultSelection).Where(x => x != defaultSelection))
		{
			results.Add(suggestion);
		}

		return results;
	}
}
