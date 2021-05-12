using Splat;
using System.Reactive.Disposables;

namespace WalletWasabi.Gui.Controls.LockScreen
{
	public abstract class WasabiLockScreenViewModelBase : LockScreenViewModelBase
	{
		protected override void OnInitialize(CompositeDisposable disposables)
		{
			base.OnInitialize(disposables);

			Services.UiConfig.LockScreenActive = true;

			Disposable.Create(() =>
			{
				Services.UiConfig.LockScreenActive = false;
			})
			.DisposeWith(disposables);
		}
	}
}
