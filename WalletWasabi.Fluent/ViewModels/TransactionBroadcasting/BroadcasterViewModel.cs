using System.Windows.Input;
using ReactiveUI;
using WalletWasabi.Fluent.Models;
using WalletWasabi.Fluent.Models.UI;

namespace WalletWasabi.Fluent.ViewModels.TransactionBroadcasting;

[NavigationMetaData(
	Order = 5,
	Category = SearchCategory.General,
	Title = "BroadcasterViewModel_Title",
	Caption = "BroadcasterViewModel_Caption",
	Keywords = "BroadcasterViewModel_Keywords",
	IconName = "live_regular",
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
