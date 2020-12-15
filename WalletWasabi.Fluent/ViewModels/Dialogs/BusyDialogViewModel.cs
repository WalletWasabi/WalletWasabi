using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Threading.Tasks;
using ReactiveUI;

namespace WalletWasabi.Fluent.ViewModels.Dialogs
{
	public partial class BusyDialogViewModel : DialogViewModelBase<Unit>
	{
		private readonly Task _task;

		public BusyDialogViewModel(Task task)
		{
			_task = task;
		}

		protected override void OnNavigatedTo(bool inStack, CompositeDisposable disposable)
		{
			base.OnNavigatedTo(inStack, disposable);

			RxApp.MainThreadScheduler.Schedule(async () =>
			{
				await _task;
				Close();
			});
		}
	}
}