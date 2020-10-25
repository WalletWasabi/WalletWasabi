using System.Windows.Input;
using ReactiveUI;

namespace WalletWasabi.Fluent.ViewModels.Dialogs
{
	public class TestDialogViewModel : DialogViewModelBase<bool>
	{
		private IScreen _screen;
		private string _message;

		public TestDialogViewModel(IScreen screen, string message)
		{
			_screen = screen;
			_message = message;

			CancelCommand = ReactiveCommand.Create(() => Close(false));
			ConfirmCommand = ReactiveCommand.Create(() => Close(true));
		}

		public string Message
		{
			get => _message;
			set => this.RaiseAndSetIfChanged(ref _message, value);
		}

		public ICommand CancelCommand { get; }
		public ICommand ConfirmCommand { get; }

		protected override void OnDialogClosed()
		{
			_screen.Router.NavigateAndReset.Execute(new AddWalletPageViewModel(_screen));
		}

		public void Close()
		{
			// TODO: Dialog.xaml back Button binding to Close() method on base class which is protected so exception is thrown.
			Close(false);
		}
	}
}