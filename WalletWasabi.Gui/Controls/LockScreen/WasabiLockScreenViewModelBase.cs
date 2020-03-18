using ReactiveUI;
using Splat;
using System.Reactive.Disposables;
using System.Reactive.Linq;

namespace WalletWasabi.Gui.Controls.LockScreen
{
	public abstract class WasabiLockScreenViewModelBase : LockScreenViewModelBase
	{
		protected override void OnInitialize(CompositeDisposable disposables)
		{
			base.OnInitialize(disposables);

			var global = Locator.Current.GetService<Global>();

			global.UiConfig.LockScreenActive = true;

			Disposable.Create(() =>
			{
				var global = Locator.Current.GetService<Global>();

				global.UiConfig.LockScreenActive = false;
			})
			.DisposeWith(disposables);
		}
	}
}
