using NBitcoin;
using System.Collections.Generic;
using System.Collections.Immutable;
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
	[AutoNotify] private string? _differenceFiatText;

	public ChangeAvoidanceSuggestionViewModel(
		decimal originalAmount,
		BuildTransactionResult transactionResult,
		decimal fiatExchangeRate)
	{
		TransactionResult = transactionResult;

		var totalAmount = GetAmount();
		var total = totalAmount.ToDecimal(MoneyUnit.BTC);
		var fiatTotal = total * fiatExchangeRate;
		var fiatOriginal = originalAmount * fiatExchangeRate;
		var fiatDifference = fiatTotal - fiatOriginal;

		_amountFiat = fiatTotal;
		_differenceFiatText = GetDifferenceFiatText(fiatDifference);
		_amount = $"{totalAmount.ToFormattedString()} BTC";
	}

	public BuildTransactionResult TransactionResult { get; }

	public Money GetAmount()
	{
		return TransactionResult.CalculateDestinationAmount();
	}

	private string GetDifferenceFiatText(decimal fiatDifference)
	{
		return fiatDifference switch
		{
			> 0 => $"{fiatDifference.ToUsd()} More",
			< 0 => $"{Math.Abs(fiatDifference).ToUsd()} Less",
			_ => "Same Amount"
		};
	}

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

		await foreach (IEnumerable<SmartCoin> selection in selectionsTask.ConfigureAwait(false))
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
				yield return new ChangeAvoidanceSuggestionViewModel(
					transactionInfo.Amount.ToDecimal(MoneyUnit.BTC),
					transaction,
					usdExchangeRate);
			}
		}
	}
}
