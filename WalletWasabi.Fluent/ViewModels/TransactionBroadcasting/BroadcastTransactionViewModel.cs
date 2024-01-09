using System.Threading.Tasks;
using ReactiveUI;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Fluent.Models.UI;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Logging;

namespace WalletWasabi.Fluent.ViewModels.TransactionBroadcasting;

[NavigationMetaData(Title = "Broadcast Transaction")]
public partial class BroadcastTransactionViewModel : RoutableViewModel
{
	public BroadcastTransactionViewModel(UiContext uiContext, SmartTransaction transaction)
	{
		UiContext = uiContext;

		SetupCancel(enableCancel: true, enableCancelOnEscape: true, enableCancelOnPressed: true);

		EnableBack = false;

		NextCommand = ReactiveCommand.CreateFromTask(async () => await OnNextAsync(transaction));

		EnableAutoBusyOn(NextCommand);

		var broadcastInfo = UiContext.TransactionBroadcaster.GetBroadcastInfo(transaction);
		TransactionId = broadcastInfo.TransactionId;
		OutputAmountString = broadcastInfo.OutputAmountString;
		InputAmountString = broadcastInfo.InputAmoutString;
		FeeString = broadcastInfo.FeeString;
		InputCount = broadcastInfo.InputCount;
		OutputCount = broadcastInfo.OutputCount;
	}

	public string? TransactionId { get; set; }

	public string? OutputAmountString { get; set; }

	public string? InputAmountString { get; set; }

	public string FeeString { get; set; } = "Unknown";

	public int InputCount { get; set; }

	public int OutputCount { get; set; }

	private async Task OnNextAsync(SmartTransaction transaction)
	{
		try
		{
			await UiContext.TransactionBroadcaster.SendAsync(transaction);
			Navigate().To().Success("The transaction has been successfully broadcasted.");
		}
		catch (Exception ex)
		{
			Logger.LogError(ex);
			await ShowErrorAsync("Broadcast Transaction", ex.ToUserFriendlyString(), "It was not possible to broadcast the transaction.");
		}
	}
}
