using ReactiveUI;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using WalletWasabi.Fluent.ViewModels.Dialogs.Base;

namespace WalletWasabi.Fluent.ViewModels.Dialogs;

public class ShuttingDownViewModel : DialogViewModelBase<Unit>
{
	private readonly ApplicationViewModel _applicationViewModel;
	private string _title;

	public ShuttingDownViewModel(ApplicationViewModel applicationViewModel)
	{
		_title = "Wasabi is shutting down...";
		_applicationViewModel = applicationViewModel;
	}

	public override string Title
	{
		get => _title;
		protected set => this.RaiseAndSetIfChanged(ref _title, value);
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
