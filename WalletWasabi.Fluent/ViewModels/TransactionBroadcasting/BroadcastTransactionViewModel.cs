using System.Linq;
using System.Threading.Tasks;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Extensions;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.Models;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Logging;

namespace WalletWasabi.Fluent.ViewModels.TransactionBroadcasting;

[NavigationMetaData(
	Title = "Broadcaster",
	Caption = "Broadcast your transactions here",
	IconName = "live_regular",
	Order = 5,
	Category = "General",
	Keywords = new[] { "Transaction Id", "Input", "Output", "Amount", "Network", "Fee", "Count", "BTC", "Signed", "Paste", "Import", "Broadcast", "Transaction", },
	NavBarPosition = NavBarPosition.None,
	NavigationTarget = NavigationTarget.DialogScreen)]
public partial class BroadcastTransactionViewModel : RoutableViewModel
{
	public BroadcastTransactionViewModel(Network network, SmartTransaction transaction)
	{
		SetupCancel(enableCancel: true, enableCancelOnEscape: true, enableCancelOnPressed: true);

		EnableBack = false;

		NextCommand = ReactiveCommand.CreateFromTask(async () => await OnNextAsync(transaction));

		EnableAutoBusyOn(NextCommand);

		ProcessTransaction(transaction, network);
	}

	public string? TransactionId { get; set; }

	public string? OutputAmountString { get; set; }

	public string? InputAmountString { get; set; }

	public string? FeeString { get; set; }

	public int InputCount { get; set; }

	public int OutputCount { get; set; }

	private void ProcessTransaction(SmartTransaction transaction, Network network)
	{
		var nullMoney = new Money(-1L);
		var nullOutput = new TxOut(nullMoney, Script.Empty);

		var psbt = PSBT.FromTransaction(transaction.Transaction, network);

		TxOut GetOutput(OutPoint outpoint) =>
			Services.BitcoinStore.TransactionStore.TryGetTransaction(outpoint.Hash, out var prevTxn)
				? prevTxn.Transaction.Outputs[outpoint.N]
				: nullOutput;

		var inputAddressAmount = psbt.Inputs
			.Select(x => x.PrevOut)
			.Select(GetOutput)
			.ToArray();

		var outputAddressAmount = psbt.Outputs
			.Select(x => x.GetCoin().TxOut)
			.ToArray();

		var psbtTxn = psbt.GetOriginalTransaction();

		TransactionId = psbtTxn.GetHash().ToString();

		InputCount = inputAddressAmount.Length;
		var totalInputValue = inputAddressAmount.Any(x => x.Value == nullMoney)
			? null
			: inputAddressAmount.Select(x => x.Value).Sum();
		InputAmountString = totalInputValue is null ? "Unknown" : $"{totalInputValue.ToFormattedString()} BTC";

		OutputCount = outputAddressAmount.Length;
		var totalOutputValue = outputAddressAmount.Any(x => x.Value == nullMoney)
			? null
			: outputAddressAmount.Select(x => x.Value).Sum();
		OutputAmountString = totalOutputValue is null ? "Unknown" : $"{totalOutputValue.ToFormattedString()} BTC";

		var networkFee = totalInputValue is null || totalOutputValue is null
			? null
			: totalInputValue - totalOutputValue;

		FeeString = networkFee?.ToFeeDisplayUnitString() ?? "Unknown";
	}

	private async Task OnNextAsync(SmartTransaction transaction)
	{
		try
		{
			await Services.TransactionBroadcaster.SendTransactionAsync(transaction);
			Navigate().To(new SuccessViewModel("The transaction has been successfully broadcasted."));
		}
		catch (Exception ex)
		{
			Logger.LogError(ex);
			await ShowErrorAsync("Broadcast Transaction", ex.ToUserFriendlyString(), "It was not possible to broadcast the transaction.");
		}
	}
}
