using System.Windows.Input;
using ReactiveUI;
using WalletWasabi.Fluent.Models.UI;

namespace WalletWasabi.Fluent.ViewModels.TransactionBroadcasting;

[NavigationMetaData(
	Title = "Broadcaster",
	Caption = "Broadcast your transaction here",
	IconName = "live_regular",
	Order = 5,
	Category = "General",
	Keywords = new[] { "Transaction Id", "Input", "Output", "Amount", "Network", "Fee", "Count", "BTC", "Signed", "Paste", "Import", "Broadcast", "Transaction", },
	NavBarPosition = NavBarPosition.None,
	NavigationTarget = NavigationTarget.DialogScreen)]
public partial class BroadcasterViewModel : TriggerCommandViewModel
{
	public BroadcasterViewModel(UiContext uiContext)
	{
		UiContext = uiContext;
	}

	public override ICommand TargetCommand => ReactiveCommand.CreateFromTask(async () =>
	{
		var dialogResult = await Navigate().To().LoadTransaction().GetResultAsync();

		if (dialogResult is { } transaction)
		{
			Navigate().To().BroadcastTransaction(transaction);
		}
	});
}
