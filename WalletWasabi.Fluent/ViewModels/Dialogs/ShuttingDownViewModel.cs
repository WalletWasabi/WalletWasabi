using ReactiveUI;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using WalletWasabi.Fluent.ViewModels.Dialogs.Base;

namespace WalletWasabi.Fluent.ViewModels.Dialogs;

[NavigationMetaData(Title = "Please wait to shut down...")]
public partial class ShuttingDownViewModel : DialogViewModelBase<Unit>
{
	private readonly ApplicationViewModel _applicationViewModel;
	private IDisposable? _intervalDisposable;

	public ShuttingDownViewModel(ApplicationViewModel applicationViewModel)
	{
		_applicationViewModel = applicationViewModel;

		NextCommand = ReactiveCommand.Create(OnNext);
	}

	protected override void OnNavigatedTo(bool isInHistory, CompositeDisposable disposables)
	{
		_intervalDisposable =
			Observable.Interval(TimeSpan.FromSeconds(3))
					  .ObserveOn(RxApp.MainThreadScheduler)
					  .Subscribe(_ =>
					  {
						  if (_applicationViewModel.CanShutdown())
						  {
							  Close();
							  _applicationViewModel.ShutDown();
						  }
					  })
					  .DisposeWith(disposables);
	}

	private void OnNext()
	{
		_intervalDisposable?.Dispose();
		Close();
	}
}
