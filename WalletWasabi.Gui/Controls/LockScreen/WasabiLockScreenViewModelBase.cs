using ReactiveUI;
using Splat;
using System.Reactive.Disposables;
using System.Reactive.Linq;

namespace WalletWasabi.Gui.Controls.LockScreen
{
	public abstract class WasabiLockScreenViewModelBase : LockScreenViewModelBase
	{
		public WasabiLockScreenViewModelBase()
		{
			var global = Locator.Current.GetService<Global>();

			global.UiConfig
				.WhenAnyValue(x => x.LockScreenActive)
				.ObserveOn(RxApp.MainThreadScheduler)
				.BindTo(this, y => y.IsLocked)
				.DisposeWith(Disposables);

			this.WhenAnyValue(x => x.IsLocked)
				.ObserveOn(RxApp.MainThreadScheduler)
				.BindTo(global.UiConfig, y => y.LockScreenActive)
				.DisposeWith(Disposables);

			IsLocked = global.UiConfig.LockScreenActive;
		}
	}
}
