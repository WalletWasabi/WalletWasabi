using ReactiveUI;
using WalletWasabi.Fluent.ViewModels.Dialogs.Base;

namespace WalletWasabi.Fluent.ViewModels.Dialogs;

[NavigationMetaData(Title = "Privacy Warning", NavigationTarget = NavigationTarget.CompactDialogScreen)]
public partial class ConfirmOpenLinkViewModel : DialogViewModelBase<bool>
{
	public ConfirmOpenLinkViewModel(UiContext uiContext, string link) : base(uiContext)
	{
		Link = link;

		NextCommand = ReactiveCommand.Create(() => Close(result: true));
		CancelCommand = ReactiveCommand.Create(() => Close(DialogResultKind.Cancel));

		SetupCancel(enableCancel: false, enableCancelOnEscape: true, enableCancelOnPressed: true);
	}

	public string Link { get; }
}
