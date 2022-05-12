using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using WalletWasabi.Fluent.ViewModels.Dialogs.Base;

namespace WalletWasabi.Fluent.ViewModels.Dialogs;

[NavigationMetaData(Title = "Wasabi is shutting down...")]
public partial class ShuttingDownViewModel : DialogViewModelBase<Unit>
{
	private readonly ApplicationViewModel _applicationViewModel;

	public ShuttingDownViewModel(ApplicationViewModel applicationViewModel)
	{
		_applicationViewModel = applicationViewModel;
	}

	protected override void OnNavigatedTo(bool isInHistory, CompositeDisposable disposables)
	{
		Observable.Interval(TimeSpan.FromSeconds(3))
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
}
