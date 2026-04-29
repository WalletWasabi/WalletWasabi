using WalletWasabi.Fluent.ViewModels.Dialogs.Base;
using Unit = System.Reactive.Unit;

namespace WalletWasabi.Fluent.ViewModels.Dialogs.ReleaseHighlights;

[NavigationMetaData(NavigationTarget = NavigationTarget.DialogScreen, Title = "")]
public partial class ReleaseHighlightsDialogViewModel: DialogViewModelBase<Unit>
{
	public ReleaseHighlightsDialogViewModel(UiContext uiContext) : base(uiContext)
	{
		ReleaseHighlights = uiContext.ReleaseHighlights;

		NextCommand = ReactiveCommand.Create(() => Close());
		CancelCommand = ReactiveCommand.Create(() => Close(DialogResultKind.Cancel));

		SetupCancel(enableCancel: false, enableCancelOnEscape: true, enableCancelOnPressed: true);
	}

	public Announcements.ReleaseHighlights ReleaseHighlights { get; }
}
