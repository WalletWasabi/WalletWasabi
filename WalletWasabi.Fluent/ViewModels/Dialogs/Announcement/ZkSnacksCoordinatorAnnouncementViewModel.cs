using System.Windows.Input;
using ReactiveUI;
using WalletWasabi.Fluent.Models.UI;

namespace WalletWasabi.Fluent.ViewModels.Dialogs.Announcement;

[NavigationMetaData(Title = "Announcement", NavigationTarget = NavigationTarget.CompactDialogScreen)]
public partial class ZkSnacksCoordinatorAnnouncementViewModel : AnnouncementBase
{
	public ZkSnacksCoordinatorAnnouncementViewModel(UiContext uiContext)
	{
		UiContext = uiContext;
		SetupCancel(enableCancel: false, enableCancelOnEscape: true, enableCancelOnPressed: true);
		EnableBack = false;
		NextCommand = CancelCommand;

		OpenSettingsCommand = ReactiveCommand.CreateFromTask(async () =>
		{
			Close();

			if (UiContext.MainViewModel is { } mainViewModel)
			{
				await mainViewModel.SettingsPage.ActivateCoinjoinTabWithFocusOnCoordinatorUri();
			}
		});
	}

	public ICommand OpenSettingsCommand { get; }

	protected override void OnDialogClosed()
	{
		base.OnDialogClosed();
		UiContext.ApplicationSettings.ShowCoordinatorAnnouncement = false;
	}
}
