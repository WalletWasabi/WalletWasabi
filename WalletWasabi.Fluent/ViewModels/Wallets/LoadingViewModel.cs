using System.Reactive.Disposables;

namespace WalletWasabi.Fluent.ViewModels.Wallets
{
	public partial class LoadingViewModel : ActivatableViewModel
	{
		[AutoNotify] private double _percent;
		[AutoNotify] private string? _statusText;

		public LoadingViewModel()
		{
			_statusText = "";
			_percent = 0;
		}

		protected override void OnActivated(CompositeDisposable disposables)
		{
			base.OnActivated(disposables);
		}
	}
}
