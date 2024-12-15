using ReactiveUI;
using WalletWasabi.Fluent.Models.UI;
using WalletWasabi.Fluent.ViewModels.Dialogs.Base;
using WalletWasabi.Helpers;
using Unit = System.Reactive.Unit;

namespace WalletWasabi.Fluent.ViewModels.Dialogs.ReleaseHighlights;

[NavigationMetaData(NavigationTarget = NavigationTarget.DialogScreen, Title = "")]
public partial class ReleaseHighlightsDialogViewModel: DialogViewModelBase<Unit>
{
	public ReleaseHighlightsDialogViewModel(UiContext uiContext)
	{
		ReleaseHighlights = uiContext.ReleaseHighlights;
		uiContext.ApplicationSettings.LastVersionHighlightsDisplayed = Constants.ClientVersion;

		NextCommand = ReactiveCommand.Create(() => Close());
		CancelCommand = ReactiveCommand.Create(() => Close(DialogResultKind.Cancel));

		SetupCancel(enableCancel: false, enableCancelOnEscape: true, enableCancelOnPressed: true);
	}

	public WalletWasabi.ReleaseHighlights.ReleaseHighlights ReleaseHighlights { get; }
}
