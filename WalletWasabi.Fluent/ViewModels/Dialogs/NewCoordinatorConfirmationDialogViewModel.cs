using System.Windows.Input;
using WalletWasabi.Discoverability;
using WalletWasabi.Fluent.ViewModels.Dialogs.Base;
using WalletWasabi.Helpers;

namespace WalletWasabi.Fluent.ViewModels.Dialogs;

[NavigationMetaData(Title = "Coordinator detected", NavigationTarget = NavigationTarget.DialogScreen)]
public partial class NewCoordinatorConfirmationDialogViewModel : DialogViewModelBase<bool>
{
	public NewCoordinatorConfirmationDialogViewModel(UiContext uiContext, CoordinatorConnectionString coordinatorConnection) : base(uiContext)
	{
		CoordinatorConnection = coordinatorConnection;
		EnableBack = false;
		SetupCancel(enableCancel: false, enableCancelOnEscape: true, enableCancelOnPressed: true);

		NextCommand = ReactiveCommand.Create(() => Close(result: true));
		OpenReadMoreCommand = ReactiveCommand.CreateFromTask(async () => await IoHelpers.OpenBrowserAsync(coordinatorConnection.ReadMore.ToString()));
	}

	public CoordinatorConnectionString CoordinatorConnection { get; }

	public ICommand OpenReadMoreCommand { get; }
}
