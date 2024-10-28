using System.Threading.Tasks;
using ReactiveUI;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Fluent.Models;
using WalletWasabi.Fluent.Models.UI;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Logging;

namespace WalletWasabi.Fluent.ViewModels.TransactionBroadcasting;

public partial class BroadcastTransactionViewModel : RoutableViewModel
{
	public BroadcastTransactionViewModel(UiContext uiContext, SmartTransaction transaction)
	{
		Title = Lang.Resources.BroadcastTransactionViewModel_Title;

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
			await ShowErrorAsync(
				Lang.Resources.BroadcastTransactionViewModel_Title,
				ex.ToUserFriendlyString(),
				Lang.Resources.BroadcastTransactionViewModel_Error_Generic_Caption);
		}
	}
}
