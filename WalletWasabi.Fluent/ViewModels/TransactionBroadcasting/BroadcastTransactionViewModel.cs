using System.Threading.Tasks;
using ReactiveUI;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Fluent.Models;
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

		BroadcastInfo = UiContext.TransactionBroadcaster.GetBroadcastInfo(transaction);
	}

	public TransactionBroadcastInfo BroadcastInfo { get; }

	private async Task OnNextAsync(SmartTransaction transaction)
	{
		try
		{
			await UiContext.TransactionBroadcaster.SendAsync(transaction);
			Navigate().To().Success();
		}
		catch (Exception ex)
		{
			Logger.LogError(ex);
			await ShowErrorAsync("Broadcast Transaction", ex.ToUserFriendlyString(), "It was not possible to broadcast the transaction.");
		}
	}
}
