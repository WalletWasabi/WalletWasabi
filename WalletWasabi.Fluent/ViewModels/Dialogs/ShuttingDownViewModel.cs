using ReactiveUI;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using WalletWasabi.Fluent.ViewModels.Navigation;

namespace WalletWasabi.Fluent.ViewModels.Dialogs;

[NavigationMetaData(Title = "Please wait to shut down...", NavigationTarget = NavigationTarget.CompactDialogScreen)]
public partial class ShuttingDownViewModel : RoutableViewModel
{
	private readonly ApplicationViewModel _applicationViewModel;
	private readonly bool _restart;

	private ShuttingDownViewModel(ApplicationViewModel applicationViewModel, bool restart)
	{
		_applicationViewModel = applicationViewModel;
		_restart = restart;
		NextCommand = CancelCommand;
	}

	protected override void OnNavigatedTo(bool isInHistory, CompositeDisposable disposables)
	{
		Observable
			.Interval(TimeSpan.FromSeconds(3))
			.ObserveOn(RxApp.MainThreadScheduler)
			.Subscribe(_ =>
			{
				if (_applicationViewModel.CoinJoinCanShutdown())
				{
					Navigate().Clear();
					_applicationViewModel.Shutdown(_restart);
				}
			})
			.DisposeWith(disposables);
	}
}
