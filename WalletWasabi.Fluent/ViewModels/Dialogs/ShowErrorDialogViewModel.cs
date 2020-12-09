using ReactiveUI;

namespace WalletWasabi.Fluent.ViewModels.Dialogs
{
	public class ShowErrorDialogViewModel : DialogViewModelBase<bool>
	{
		public ShowErrorDialogViewModel(string message)
		{
			Message = message;

			NextCommand = ReactiveCommand.Create(() => Close());
		}

		public string Message { get; }
	}
}