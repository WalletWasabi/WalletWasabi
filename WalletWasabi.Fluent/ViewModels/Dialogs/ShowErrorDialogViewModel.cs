using ReactiveUI;
using WalletWasabi.Fluent.ViewModels.Dialogs.Base;

namespace WalletWasabi.Fluent.ViewModels.Dialogs;

public class ShowErrorDialogViewModel : DialogViewModelBase<bool>
{
	public ShowErrorDialogViewModel(string message, string title, string caption)
	{
		Message = message;
		Title = title;
		Caption = caption;

		NextCommand = ReactiveCommand.Create(() => Close());

		SetupCancel(enableCancel: false, enableCancelOnEscape: true, enableCancelOnPressed: true);
	}

	public string Message { get; }
}
