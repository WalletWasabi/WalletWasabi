using NBitcoin;
using WalletWasabi.Blockchain.TransactionBuilding;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Helpers;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Send;

public partial class ChangeAvoidanceSuggestionViewModel : SuggestionViewModel
{
	private const int SignificantFiguresForFiatAmount = 3;
	[AutoNotify] private string _amount;
	[AutoNotify] private string _amountFiat;
	[AutoNotify] private string? _differenceFiat;

	public ChangeAvoidanceSuggestionViewModel(
		decimal originalAmount,
		BuildTransactionResult transactionResult,
		decimal fiatExchangeRate)
	{
		TransactionResult = transactionResult;

		var totalAmount = transactionResult.CalculateDestinationAmount();
		var total = totalAmount.ToDecimal(MoneyUnit.BTC);

		_amountFiat = total.RoundToSignificantFigures(SignificantFiguresForFiatAmount).GenerateFiatText(fiatExchangeRate, "USD");

		var fiatTotal = total * fiatExchangeRate;
		var fiatOriginal = originalAmount * fiatExchangeRate;
		var fiatDifference = fiatTotal - fiatOriginal;
		var roundedFiatDifference = fiatDifference.RoundToSignificantFigures(SignificantFiguresForFiatAmount);

		_differenceFiat = (fiatDifference > 0
				? $"{roundedFiatDifference.GenerateFiatText("USD")} More"
				: $"{Math.Abs(roundedFiatDifference).GenerateFiatText("USD")} Less")
			.Replace("(", "").Replace(")", "");

		_amount = $"{totalAmount.ToFormattedString()} BTC";
	}

	public BuildTransactionResult TransactionResult { get; }
}
