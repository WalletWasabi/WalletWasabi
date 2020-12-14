using System.Reactive.Linq;
using ReactiveUI;

namespace WalletWasabi.Fluent.ViewModels.Dialogs
{
	public partial class TestDialogViewModel : DialogViewModelBase<bool>
	{
		[AutoNotify] private string _message;

		public TestDialogViewModel(string message)
		{
			Title = "Test Dialog";
			_message = message;

			BackCommand = ReactiveCommand.Create(
				() => Navigate().Back(),
				this.WhenAnyValue(x => x.IsDialogOpen).ObserveOn(RxApp.MainThreadScheduler));
			CancelCommand = ReactiveCommand.Create(
				() => Close(result: false),
				this.WhenAnyValue(x => x.IsDialogOpen).ObserveOn(RxApp.MainThreadScheduler));
			NextCommand = ReactiveCommand.Create(
				() => Close(result: true),
				this.WhenAnyValue(x => x.IsDialogOpen).ObserveOn(RxApp.MainThreadScheduler));
		}

		protected override void OnDialogClosed()
		{
		}

		public void Close()
		{
			Close(result: false);
		}
	}
}