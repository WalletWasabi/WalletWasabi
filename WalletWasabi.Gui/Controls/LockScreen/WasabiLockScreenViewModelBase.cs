using Splat;
using System.Reactive.Disposables;

namespace WalletWasabi.Gui.Controls.LockScreen
{
	public abstract class WasabiLockScreenViewModelBase : LockScreenViewModelBase
	{
		protected override void OnInitialize(CompositeDisposable disposables)
		{
			base.OnInitialize(disposables);

			var global = Locator.Current.GetService<Global>();

			global.UiConfig.LockScreenActive = true;

			_ = Disposable.Create(() =>
			  {
				  var global = Locator.Current.GetService<Global>();

				  global.UiConfig.LockScreenActive = false;
			  })
			.DisposeWith(disposables);
		}
	}
}
