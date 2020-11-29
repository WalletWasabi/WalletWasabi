using System.Reactive.Linq;
using ReactiveUI;
using WalletWasabi.Fluent.ViewModels.Navigation;

namespace WalletWasabi.Fluent.ViewModels.Dialogs
{
	public class TestDialogViewModel : DialogViewModelBase<bool>
	{
		private string _message;

		public TestDialogViewModel(string message)
		{
			_message = message;

			var backCommandCanExecute = this.WhenAnyValue(x => x.IsDialogOpen).ObserveOn(RxApp.MainThreadScheduler);

			var cancelCommandCanExecute = this.WhenAnyValue(x => x.IsDialogOpen).ObserveOn(RxApp.MainThreadScheduler);

			var nextCommandCanExecute = this.WhenAnyValue(x => x.IsDialogOpen).ObserveOn(RxApp.MainThreadScheduler);

			BackCommand = ReactiveCommand.Create(GoBack, backCommandCanExecute);
			CancelCommand = ReactiveCommand.Create(() => Close(false), cancelCommandCanExecute);
			NextCommand = ReactiveCommand.Create(() => Close(true), nextCommandCanExecute);
		}

		public string Message
		{
			get => _message;
			set => this.RaiseAndSetIfChanged(ref _message, value);
		}

		protected override void OnDialogClosed()
		{
			// TODO: Disable when using Dialog inside DialogScreenViewModel / Settings
			// NavigateTo(new SettingsPageViewModel(NavigationState), NavigationTarget.HomeScreen, true);
		}

		public void Close()
		{
			// TODO: Dialog.xaml back Button binding to Close() method on base class which is protected so exception is thrown.
			Close(false);
		}
	}
}