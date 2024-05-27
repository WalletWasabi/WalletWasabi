using ReactiveUI;
using WalletWasabi.Fluent.Models.UI;

namespace WalletWasabi.Fluent.ViewModels.Dialogs.Announcement;

[NavigationMetaData(Title = "Coinjoins stopped", NavigationTarget = NavigationTarget.DialogScreen)]
public partial class ZkSnacksCoordinatorAnnouncementViewModel : AnnouncementBase
{
	public ZkSnacksCoordinatorAnnouncementViewModel(UiContext uiContext)
	{
		UiContext = uiContext;
		SetupCancel(enableCancel: false, enableCancelOnEscape: true, enableCancelOnPressed: true);
		EnableBack = false;
		NextCommand = CancelCommand;
	}

	protected override void OnDialogClosed()
	{
		base.OnDialogClosed();
		UiContext.ApplicationSettings.ShowCoordinatorAnnouncement = false;
	}
}
