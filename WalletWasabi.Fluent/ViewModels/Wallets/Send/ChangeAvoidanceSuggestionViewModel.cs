using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using NBitcoin;
using WalletWasabi.Blockchain.TransactionBuilding;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Logging;
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
		TransactionInfo transactionInfo, BitcoinAddress destination, Wallet wallet, BuildTransactionResult requestedTransaction, [EnumeratorCancellation] CancellationToken cancellationToken)
	{
		Task<ChangeAvoidanceSuggestionViewModel?> bnbSuggestionTask = Task.Run(() =>
		{
			List<SmartCoin> availableCoins = wallet.Coins.Unspent().ToList();

			if (ChangelessTransactionCoinSelector.TryGetCoins(availableCoins, transactionInfo.FeeRate, transactionInfo.Amount, out IEnumerable<SmartCoin>? selection, cancellationToken))
			{
				BuildTransactionResult transaction = TransactionHelpers.BuildChangelessTransaction(
					wallet,
					destination,
					transactionInfo.UserLabels,
					transactionInfo.FeeRate,
					selection,
					tryToSign: false);

				return new ChangeAvoidanceSuggestionViewModel(
					transactionInfo.Amount.ToDecimal(MoneyUnit.BTC),
					transaction,
					wallet.Synchronizer.UsdExchangeRate,
					isOriginal: false);
			}

			return null;
		});

		ChangeAvoidanceSuggestionViewModel? bnbSuggestion = await bnbSuggestionTask;

		if (bnbSuggestion is not null)
		{
			yield return bnbSuggestion;
		}
	}
}
