using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;
using System.Reactive.Concurrency;
using WalletWasabi.Fluent.ViewModels.Navigation;

namespace WalletWasabi.Fluent.ViewModels.Dialogs;

[NavigationMetaData(Title = "Please wait to shut down...", NavigationTarget = NavigationTarget.CompactDialogScreen)]
public partial class ShuttingDownViewModel : RoutableViewModel
{
	private readonly ApplicationViewModel _applicationViewModel;
	private readonly bool _restart;

	public ShuttingDownViewModel(UiContext uiContext, ApplicationViewModel applicationViewModel, bool restart) : base(uiContext)
	{
		_applicationViewModel = applicationViewModel;
		_restart = restart;

		NextCommand = ReactiveCommand.CreateFromTask(
			async () =>
			{
				await UiContext.CoinjoinModel.RestartAbortedCoinjoinsAsync();
				Navigate().Clear();
			});
	}

	protected override void OnNavigatedTo(bool isInHistory, CompositeDisposable disposables)
	{
		RxSchedulers.MainThreadScheduler.Schedule(async () => await UiContext.CoinjoinModel.SignalToStopCoinjoinsAsync());

		Observable.Interval(TimeSpan.FromSeconds(3))
				  .ObserveOn(RxSchedulers.MainThreadScheduler)
				  .Subscribe(_ =>
				  {
					  if (UiContext.CoinjoinModel.CanShutdown())
					  {
						  Navigate().Clear();
						  _applicationViewModel.Shutdown(_restart);
					  }
				  })
				  .DisposeWith(disposables);
	}
}
