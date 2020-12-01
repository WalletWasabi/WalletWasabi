using System.Reactive.Linq;
using ReactiveUI;

namespace WalletWasabi.Fluent.ViewModels.Dialogs
{
	public class TestDialogViewModel : DialogViewModelBase<bool>
	{
		private string _message;

		public TestDialogViewModel(string message)
		{
			_message = message;

			BackCommand = ReactiveCommand.Create(
				() => Navigate().Back(),
				this.WhenAnyValue(x => x.IsDialogOpen).ObserveOn(RxApp.MainThreadScheduler));
			CancelCommand = ReactiveCommand.Create(
				() => Close(false),
				this.WhenAnyValue(x => x.IsDialogOpen).ObserveOn(RxApp.MainThreadScheduler));
			NextCommand = ReactiveCommand.Create(
				() => Close(true),
				this.WhenAnyValue(x => x.IsDialogOpen).ObserveOn(RxApp.MainThreadScheduler));
		}

		public string Message
		{
			get => _message;
			set => this.RaiseAndSetIfChanged(ref _message, value);
		}

		protected override void OnDialogClosed()
		{
		}

		public void Close()
		{
			Close(false);
		}
	}
}