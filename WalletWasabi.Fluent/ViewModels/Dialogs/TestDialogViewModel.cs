using System.Windows.Input;
using ReactiveUI;

namespace WalletWasabi.Fluent.ViewModels.Dialogs
{
	public class TestDialogViewModel : DialogViewModelBase<bool>
	{
		private NavigationStateViewModel _navigationState;
		private string _message;

		public TestDialogViewModel(NavigationStateViewModel navigationState, string message)
		{
			_navigationState = navigationState;
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
			_navigationState.MainScreen().Router.NavigateAndReset.Execute(new HomePageViewModel(_navigationState));
		}

		public void Close()
		{
			// TODO: Dialog.xaml back Button binding to Close() method on base class which is protected so exception is thrown.
			Close(false);
		}
	}
}