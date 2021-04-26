using System.Reactive.Disposables;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Home.Tiles
{
	public abstract class TileViewModel : ViewModelBase
	{
		protected virtual void OnActivated(CompositeDisposable disposables)
		{

		}

		public void Activate(CompositeDisposable disposables)
		{
			OnActivated(disposables);
		}
	}
}